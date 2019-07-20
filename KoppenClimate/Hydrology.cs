using System;
using System.IO;
using OSGeo.GDAL;

namespace KoppenClimate
{
    class Hydrology
	{
        static int SEA_LEVEL = 0; // -125;
        static byte ICE = (byte)'I';

        private byte dir = 0;

        public void readBed(short[] elevation)
        {
            String inFileBed = @"data\elevation\ETOPO1_Bed_g_geotiff.tif";

            Dataset dsBed = Gdal.Open(inFileBed, Access.GA_ReadOnly);
            Band baBed = dsBed.GetRasterBand(1);

            Console.WriteLine("Elevation File: " + inFileBed);

            // Get the width and height of the Dataset - assuming all sizes the same
            int width = baBed.XSize;
            int height = baBed.YSize;

            short[] elevation_deviation = new short[width * height];
            byte[] elevation_deviation_type = new byte[width * height];

            baBed.ReadRaster(0, 0, width, height, elevation_deviation, width, height, 0, 0);

            // TODO create the deviation separatelly and save to file. To be loaded as elevation_deviation...
            getIceDeviation(elevation, elevation_deviation, elevation_deviation, elevation_deviation_type);

        }

        public void init()
        {
            GdalConfiguration.ConfigureGdal();

            Gdal.AllRegister();
        }

        public void run()
        {
            String inFileIce = @"tmp_TEST.tif";
            // String inFileIce = @"data\elevation\ETOPO1_Ice_g_geotiff.tif";
            String outFileName = @"tmp_FLOW.tif";

            Dataset dsIce = Gdal.Open(inFileIce, Access.GA_ReadOnly);
            Band baIce = dsIce.GetRasterBand(1);

            Console.WriteLine("Elevation deviation File: " + inFileIce);

            // Get the width and height of the Dataset - assuming all sizes the same
            int width = baIce.XSize;
            int height = baIce.YSize;

            byte[] flowDirection = new byte[width * height];
            short[] flowAccumulation = new short[width * height];
            byte[] watershed = new byte[width * height];

            short[] elevation = new short[width * height];

            baIce.ReadRaster(0, 0, width, height, elevation, width, height, 0, 0);

            Console.WriteLine("Calculate flow directions");
            calculateFlowDirection(width, height, elevation, flowDirection);

            Console.WriteLine("Calculate watersheds");
            calculateWatershed(width, height, flowDirection, watershed);

            Console.WriteLine("Calculate Fills");
            calculateFills(width, height, elevation, flowDirection, watershed);

            //Console.WriteLine("Calculate flow accumulations");
            //calculateFlowAccumulation(width, height, elevation, elevation_deviation, flowDirection, flowAccumulation);

            if (File.Exists(outFileName))
            {
                File.Delete(outFileName);
            }
            Driver drv = Gdal.GetDriverByName("GTiff");
            string[] options = new string[] { "BLOCKXSIZE=" + width, "BLOCKYSIZE=" + 1 };
            Dataset ds = drv.Create(outFileName, width, height, 1, DataType.GDT_Byte, options);
            // Dataset ds = drv.Create(outFileName, width, height, 1, DataType.GDT_Int16, options);
            double[] argout = new double[4];
            dsIce.GetGeoTransform(argout);
            ds.SetGeoTransform(argout);
            ds.SetGCPs(dsIce.GetGCPs(), "");
            Band ba = ds.GetRasterBand(1);

            ba.WriteRaster(0, 0, width, height, watershed, width, height, 0, 0);

            ba.FlushCache();
            ds.FlushCache();

            Console.WriteLine("Done");
        }

        private void calculateFlowAccumulation(int width, int height, short[] elevation, short[] elevation_deviation, byte[] flowDirection, short[] flowAccumulation)
        {
            for (int i = 0; i < flowAccumulation.Length; i++)
            {
                flowAccumulation[i] = 1;
            }

            getFlowAccumulation(width, height, elevation, flowDirection, flowAccumulation);

        }

        private void getFlowAccumulation(int width, int height, short[] elevation, byte[] flowDirection, short[] flowAccumulation)
        {
            int maxMaxLength = 0;

            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int i = x + y * width;

                    // decrement cell count of 1
                    flowAccumulation[i]--;
                    int maxLength = 0;

                    // follow sell flow track
                    while (flowDirection[i] != 0)
                    {
                        maxLength++;

                        // Console.WriteLine(maxLength + " " + elevation[i] + " " + flowDirection[i] + " " + i);

                        switch (flowDirection[i])
                        {
                            case (1):
                                i = i + 1;
                                flowAccumulation[i]++;
                                break;
                            case (2):
                                i = i + 1 - width;
                                flowAccumulation[i]++;
                                break;
                            case (4):
                                i = i - width;
                                flowAccumulation[i]++;
                                break;
                            case (8):
                                i = i - 1 - width;
                                flowAccumulation[i]++;
                                break;
                            case (16):
                                i = i - 1;
                                flowAccumulation[i]++;
                                break;
                            case (32):
                                i = i - 1 + width;
                                flowAccumulation[i]++;
                                break;
                            case (64):
                                i = i + width;
                                flowAccumulation[i]++;
                                break;
                            case (128):
                                i = i + 1 + width;
                                flowAccumulation[i]++;
                                break;
                            default: // 0
                                break;
                        }
                    }
                    if (maxMaxLength<maxLength)
                    {
                        maxMaxLength = maxLength;
                        Console.WriteLine(maxLength);
                    }
                }
            }
        }

        private void calculateFills(int width, int height, short[] elevation, byte[] flowDirection, byte[] watershed)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int i = x + y * width;

                    switch (flowDirection[i])
                    {
                        case (0):
                        case (1):
                        case (2):
                        case (4):
                        case (8):
                        case (16):
                        case (32):
                        case (64):
                        case (128):
                            break;
                        default:
                            // find the edge of the watershed 
                            int orig = i;
                            int edge = findEdge(i, width, watershed);

                            // walk along it till a coplete circumvention and 
                            // save lowest outlet that does not flow to this watershed 
                            edge = edgeWalk(edge, elevation, flowDirection, watershed[i]);

                            // redirect flow to lowest outlet if our outlet is not the ocean

                            break;
                    }
                }
            }
        }

        private int edgeWalk(int edge, short[] elevation, byte[] flowDirection, byte shed)
        {
            //throw new NotImplementedException();
            return 0;
        }

        private int findEdge(int pos, int width, byte[] watershed)
        {
            for (int i = pos; i<width; i++)
            {
                if (watershed[i] != watershed[pos])
                    return i;
            }
            return 0; // TODO
        }

        private void calculateWatershed(int width, int height, byte[] flowDirection, byte[] watershed)
        {
            byte shed = 0;

            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int i = x + y * width;

                    if (watershed[i] != 0)
                        continue;

                    if (flowDirection[i] == 0)
                        continue;

                    shed++;
                    if (shed > 254) // wrap around max value - assuming not >254 neighbouring watersheds
                        shed = 1;
                    byte tmpShed = shed;

                    bool exit = false;
                    int j = 0;
                    int[] track = new int[1000];

                    // follow sell flow track
                    while (!exit)
                    {
                        //Console.WriteLine(flowDirection[i] + " " + i + " " + j);
                        track[j] = i;
                        if (watershed[i] == tmpShed)
                        {
                            exit = true; // TODO should never happen!?
                        }
                        else if (watershed[i] != 0)
                        {
                            shed--;
                            tmpShed = watershed[i];
                            backTrack(tmpShed, watershed, track);
                        }
                        else
                        {
                            watershed[i] = tmpShed;
                        }

                        switch (flowDirection[i])
                        {
                            case (1):
                                i = i + 1;
                                break;
                            case (2):
                                i = i + 1 - width;
                                break;
                            case (4):
                                i = i - width;
                                break;
                            case (8):
                                i = i - 1 - width;
                                break;
                            case (16):
                                i = i - 1;
                                break;
                            case (32):
                                i = i - 1 + width;
                                break;
                            case (64):
                                i = i + width;
                                break;
                            case (128):
                                i = i + 1 + width;
                                break;
                            case (0): // sea
                                exit = true;
                                break;
                            default: // sinks
                                byte newShed = floodFill(tmpShed, i, width, watershed, flowDirection);
                                if (newShed != tmpShed)
                                {
                                    shed--;
                                    backTrack(newShed, watershed, track);
                                }
                                exit = true;
                                break;
                        }
                        j++;
                    }

                    
                }
            }
        }

        private void backTrack(byte tmpShed, byte[] watershed, int[] track)
        {
            for (int k = 0; k < track.Length; k++)
            {
                if (track[k] > 0)
                {
                    watershed[track[k]] = tmpShed;
                }
                else
                {
                    break;
                }
            }

        }

        private byte floodFill(byte shed, int i, int width, byte[] watershed, byte[] flowDirection)
        {
            if (flowDirectionIsSet(flowDirection[i])) return shed;
            else if (shed == watershed[i]) return shed;
            else if (0 != watershed[i]) return watershed[i];
            else watershed[i] = shed;

            byte nshed = floodFill(shed, i + 1, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i + 1 - width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i - width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i - 1 - width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i - 1, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i - 1 + width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i + width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;
            nshed = floodFill(shed, i + 1 + width, width, watershed, flowDirection);
            if (nshed != shed) return nshed;

            return shed;
        }

        private bool flowDirectionIsSet(byte v)
        {
            switch (v)
            {
                case (1):
                case (2):
                case (4):
                case (8):
                case (16):
                case (32):
                case (64):
                case (128):
                case (0): // sea
                    return true;
                default: // sinks
                    return false;
            }
        }

        private bool GetBit(byte thebyte, int position)
        {
            return (1 == ((thebyte >> position) & 1));
        }

        private void calculateFlowDirection(int width, int height, short[] elevation, byte[] flowDirection)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int i = x + y * width;

                    if (elevation[i] < SEA_LEVEL)
                    {
                        flowDirection[i] = 0;
                    } else
                    {
                        dir = 0;
                        short min = elevation[i];
                        min = setDirection(min, elevation[i + 1], 1);
                        min = setDirection(min, elevation[i + 1 - width], 2);
                        min = setDirection(min, elevation[i - width], 4);
                        min = setDirection(min, elevation[i - 1 - width], 8);
                        min = setDirection(min, elevation[i - 1], 16);
                        min = setDirection(min, elevation[i - 1 + width], 32);
                        min = setDirection(min, elevation[i + width], 64);
                        min = setDirection(min, elevation[i + 1 + width], 128);

                        // is this a single cell sink? We have to fill it - or we can't distinguish it from the sea bead. dir=0
                        if (dir == 0)
                        {
                            // TODO use second lowest cell and make it drain to it, and it drain to this
                        }

                        flowDirection[i] = dir;

                    }
                }
            }
        }

        private short setDirection(short min, short v, byte d)
        {
            if (min > v)
            {
                min = v;
                dir = d;
            } else if (min == v)
            {
                dir += d;
            }
            return min;
        }

        private void getIceDeviation(short[] a, short[] b, short[] diff, byte[] diff_type)
        {
            for (int i=0; i<a.Length; i++)
            {
                diff[i] = (short)(a[i] - b[i]);
                if (diff[i] > 0)
                    diff_type[i] = ICE;
                else
                    diff[i] = 0;
            }
        }

    }
}
