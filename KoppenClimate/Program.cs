using System;
using System.Drawing;
using System.Drawing.Imaging;
using OSGeo.GDAL;

namespace KoppenClimate
{
    class KoppenGeiger
    {
        static int NUM_MONTH = 12;
        // static int W = 43200; //  Size (43200,20880)
        // static int H = 1; 

        int[] MAT; // mean annual air temperature(°C);
        int[] Tcold; // the air temperature of the coldest month(°C);
        int[] Thot; // the air temperature of the warmest month(°C);
        int[] Tmon10; // the number of months with air temperature >= 10 °C(unitless);
        int[] MAP; // mean annual precipitation(mm y-1);
        int[] Pdry; // precipitation in the driest month(mm month - 1);
        int[] Psdry; // precipitation in the driest month in summer(mm month - 1);
        int[] Pwdry; // precipitation in the driest month in winter(mm month - 1);
        int[] Pswet; // precipitation in the wettest month in summer(mm month - 1);
        int[] Pwwet; // precipitation in the wettest month in winter(mm month - 1);
        int[] Pthreshold; // 2×MAT + 28 if > 70 % of precipitation falls in summer, otherwise Pthreshold = 2×MAT + 14.Summer(winter) is the six - month period that is warmer(colder) between April - September and October-March.
        int[] hPsummer;

        Boolean PMIP3_CNRM_CM5 = false;
        Boolean PMIP3_MPI_ESM_P = true;

        static void Main(string[] args)
        {
            if (args[0] != null && args[0].StartsWith("h"))
            {
                Hydrology hydrology = new Hydrology();
                hydrology.init();
                hydrology.run();
                return;
            }



            KoppenGeiger koppenGeiger = new KoppenGeiger();

            // CHELSA - now
            String tmeansFileNames = @"data\CHELSA\CHELSA_temp10_";
            String precsFileNames = @"data\CHELSA\CHELSA_prec_";
            String outFileName = "tmp_CHELSA.tif";

            if (koppenGeiger.PMIP3_CNRM_CM5)
            {
                tmeansFileNames = @"data\PMIP3\PMIP3_CNRM_CM5\CHELSA_PMIP_CNRM-CM5_tmean_";
                precsFileNames = @"data\PMIP3\PMIP3_CNRM_CM5\CHELSA_PMIP_CNRM-CM5_prec_";
                outFileName = "tmp_PMIP3.tif";

            } else if (koppenGeiger.PMIP3_MPI_ESM_P)
            {
                tmeansFileNames = @"data\PMIP3\PMIP3_MPI_ESM_P\CHELSA_PMIP_MPI-ESM-P_tmean_";
                precsFileNames = @"data\PMIP3\PMIP3_MPI_ESM_P\CHELSA_PMIP_MPI-ESM-P_prec_";
                outFileName = "tmp_PMIP3_MPI_ESM_P.tif";
            }

            GdalConfiguration.ConfigureGdal();

            Gdal.AllRegister();

            int width;
            int height;

            Band[] precsBands = new Band[NUM_MONTH];
            Band[] tmeansBands = new Band[NUM_MONTH];

            // for all files 
            for (int month = 1; month <= 12; month++)
            {
                String fileName = koppenGeiger.getFileNameTemp(tmeansFileNames, month);
                tmeansBands[month - 1] = koppenGeiger.getBand(fileName);

                Console.WriteLine("Tmean File: " + fileName);

                fileName = koppenGeiger.getFileNamePrec(precsFileNames, month);
                precsBands[month - 1] = koppenGeiger.getBand(fileName);

                Console.WriteLine("Prec File: " + fileName);
            }

            // Get the width and height of the Dataset - assuming all sizes the same
            width = tmeansBands[0].XSize;
            height = tmeansBands[0].YSize;

            koppenGeiger.init(width);

            String refFile = koppenGeiger.getFileNamePrec(precsFileNames, 1);
            Dataset  ds = koppenGeiger.getDataset(outFileName, refFile);
            Band ba = ds.GetRasterBand(1);

            short[] koppen = new short[width];

            short[] precs = new short[NUM_MONTH * width];
            short[] tmeans = new short[NUM_MONTH * width];
            short[] _precs = new short[width];
            short[] _tmeans = new short[width];

            int i;
            for (i = 0; i < height; i++)
            {
                for (int month = 0; month < NUM_MONTH; month++)
                {
                    tmeansBands[month].ReadRaster(0, i, width, 1, _tmeans, width, 1, 0, 0);
                    precsBands[month].ReadRaster(0, i, width, 1, _precs, width, 1, 0, 0);
                    Array.Copy(_tmeans, 0, tmeans, month * width, width);
                    Array.Copy(_precs, 0, precs, month * width, width);
                }

                // Console.WriteLine("X:Y " + i + " " + i * W);

                koppenGeiger.clear(width);
                koppenGeiger.calculateMaps(precs, tmeans, NUM_MONTH, width, i<=(height/2));
                koppenGeiger.calculateKoppen(koppen, 0, width);

                ba.WriteRaster(0, i, width, 1, koppen, width, 1, 0, 0);
            }

            ba.FlushCache();
            ds.FlushCache();
            /*
            // Create a Bitmap to store the GDAL image in
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            int j;
            for (i = 0; i < height; i++)
            {
                for (j = 0; j < width; j++)
                {
                    bitmap.SetPixel(j, i, Color.FromArgb(koppen[i*j + j]));
                }
            }

            bitmap.Save(outFileName);
            */

            Console.WriteLine("Done");
        }

        private void init(int size)
        {
            MAT = new int[size];
            Tcold = new int[size];
            Thot = new int[size];
            Tmon10 = new int[size];
            MAP = new int[size];
            Pdry = new int[size];
            Psdry = new int[size];
            Pwdry = new int[size];
            Pswet = new int[size];
            Pwwet = new int[size];
            Pthreshold = new int[size];
            hPsummer = new int[size];

            clear(size);
        }

        private void clear(int size)
        {
            int j = 0;
            for (j = 0; j < size; j++)
            {
                MAT[j] = 0;
                Tcold[j] = Int16.MaxValue;
                Thot[j] = Int16.MinValue;
                Tmon10[j] = 0;
                MAP[j] = 0;
                Pdry[j] = Int16.MaxValue;
                Psdry[j] = Int16.MaxValue;
                Pwdry[j] = Int16.MaxValue;
                Pswet[j] = 0;
                Pwwet[j] = 0;
                Pthreshold[j] = 0;
                hPsummer[j] = 0;
            }
        }

        Band getBand(String file)
        {
            /* -------------------------------------------------------------------- */
            /*      Open dataset.                                                   */
            /* -------------------------------------------------------------------- */
            Dataset ds = Gdal.Open(file, Access.GA_ReadOnly);

            // Console.WriteLine("Raster dataset parameters:");
            // Console.WriteLine("  Projection: " + ds.GetProjectionRef());
            // Console.WriteLine("  RasterCount: " + ds.RasterCount);
            // Console.WriteLine("  RasterSize (" + ds.RasterXSize + "," + ds.RasterYSize + ")");

            Band band = ds.GetRasterBand(1);
            // Console.WriteLine("Band " + 1 + " :");
            // Console.WriteLine("   DataType: " + band.DataType);
            // Console.WriteLine("   Size (" + band.XSize + "," + band.YSize + ")");
            // Console.WriteLine("   PaletteInterp: " + band.GetRasterColorInterpretation().ToString());

            return band;
        }

        private Dataset getDataset(string outFileName, string refFile)
        {
            Dataset refDs = Gdal.Open(refFile, Access.GA_ReadOnly);
            Band band = refDs.GetRasterBand(1);
            
            int bXSize, bYSize;
            int w, h;

            w = refDs.RasterXSize;
            h = refDs.RasterYSize;
            bXSize = w;
            bYSize = 1;

            Driver drv = Gdal.GetDriverByName("GTiff");

            string[] options = new string[] { "BLOCKXSIZE=" + bXSize, "BLOCKYSIZE=" + bYSize };
            Dataset ds = drv.Create(outFileName, w, h, 1, DataType.GDT_Int16, options);

            double[] argout = new double[4];
            refDs.GetGeoTransform(argout);
            ds.SetGeoTransform(argout);
            ds.SetGCPs(refDs.GetGCPs(), "");



            return ds;
        }


        void calculateMaps(short[] precs, short[] tmeans, int months, int size, Boolean isNorthenHem)
        {
            int j;

            for (int month = 0; month < months; month++)
            {
                for (j = 0; j < size; j++)
                {
                    int tmean = tmeans[j + month * size];
                    tmean = convertTemp(tmean);

                    MAT[j] += tmean;
                    Tcold[j] = Math.Min(tmean, Tcold[j]);
                    Thot[j] = Math.Max(tmean, Thot[j]);
                    Tmon10[j] += (tmean >= 10 ? 1 : 0);

                    short prec = precs[j + month * size];
                    prec = convertPrec(prec);

                    MAP[j] += prec;
                    Pdry[j] = Math.Min(prec, Pdry[j]);
                    if ((isNorthenHem && isAprilSept(month)) || (!isNorthenHem && !isAprilSept(month))) // Summer
                    {
                        Psdry[j] = Math.Min(prec, Psdry[j]);
                        Pswet[j] = Math.Max(prec, Pswet[j]);
                        hPsummer[j] += prec;
                    }
                    else // Winter
                    {
                        Pwdry[j] = Math.Min(prec, Pwdry[j]);
                        Pwwet[j] = Math.Max(prec, Pwwet[j]);
                    }

                }
            }

            for (j = 0; j < size; j++)
            {
                MAT[j] = MAT[j] / months;

                float summerPct = 0;
                if (MAP[j] != 0) {
                    summerPct = (float)hPsummer[j] / (float)MAP[j];
                }

                if (summerPct < 0.3)
                {
                    Pthreshold[j] = 2 * MAT[j];
                }
                else if (summerPct >= 0.7)
                {
                    Pthreshold[j] = 2 * MAT[j] + 28;
                }
                else
                {
                    Pthreshold[j] = 2 * MAT[j] + 14;
                }

            }
        }
        private bool isAprilSept(int i)
        {
            return i > 2 && i < 9;
        }

        void calculateKoppen(short[] koppen, int pos, int size)
        {
            int i;

            for (i = 0; i < size; i++)
            {
                if (MAT[i]<=-100 || MAP[i] < 0 || MAP[i] > 15000)
                {
                    // bad data
                    koppen[pos + i] = 0;
                    continue;
                }

                if (MAP[i] < 10 * Pthreshold[i]) // Arid
                {
                    if (MAP[i] < 5 * Pthreshold[i]) // Desert
                    {
                        if (MAT[i] >= 18) // Hot
                        {
                            koppen[pos + i] = KGClimateZones.BWh;
                        }
                        else // Cold
                        {
                            koppen[pos + i] = KGClimateZones.BWk;
                        }
                    }
                    else // Steppe
                    {
                        if (MAT[i] >= 18) // Hot
                        {
                            koppen[pos + i] = KGClimateZones.BSh;
                        }
                        else // Cold
                        {
                            koppen[pos + i] = KGClimateZones.BSk;
                        }
                    }
                }
                else if (Tcold[i] >= 18) // Tropical
                {
                    if (Pdry[i] >= 60) // Rainforest
                    {
                        koppen[pos + i] = KGClimateZones.Af;
                    }
                    else if (Pdry[i] >= (100 - MAP[i] / 25)) // Monsoon
                    {
                        koppen[pos + i] = KGClimateZones.Am;
                    }
                    else // Savannah
                    {
                        koppen[pos + i] = KGClimateZones.Aw;
                    }
                }
                else if (Thot[i] > 10 && Tcold[i] > 0 && Tcold[i] < 18) // Temperate
                {
                    if ((Psdry[i] < 40) && (Psdry[i] < (Pwwet[i] / 3))) // Dry summer 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Csa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Csb;

                        }
                        else if (Tmon10[i] >= 1 && Tmon10[i] < 4) // Cold summer 
                        {
                            koppen[pos + i] = KGClimateZones.Csc;
                        }
                        else
                        {
                            Console.WriteLine("Error: Temperate Dry summer " + Tmon10[i] + " " + Psdry[i] + " " + Pwwet[i] / 3);
                        }
                    }
                    else if (Pwdry[i] < (Pswet[i] / 10)) // Dry winter 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cwa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cwb;
                        }
                        else if (Tmon10[i] >= 1 && Tmon10[i] < 4) // Cold summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cwc;
                        }
                        else
                        {
                            Console.WriteLine("Error: Temperate Dry Winter " + Tmon10[i] + " " + Thot[i]);
                        }
                    }
                    else // Without dry season 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cfa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cfb;
                        }
                        else if (Tmon10[i] >= 1 && Tmon10[i] < 4) // Cold summer 
                        {
                            koppen[pos + i] = KGClimateZones.Cfc;
                        }
                        else
                        {
                            Console.WriteLine("Error: Temperate  Without dry season " + Tmon10[i] + " " + Thot[i]);
                        }
                    }
                }
                else if (Thot[i] > 10 && Tcold[i] <= 0) // Cold
                {
                    if (Psdry[i] >= 0 && Psdry[i] < 40 && (Psdry[i] < (Pwwet[i] / 3))) // Dry summer 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dsa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dsb;
                        }
                        else if (Tcold[i] < -38) // Very cold winter 
                        {
                            koppen[pos + i] = KGClimateZones.Dsd;
                        }
                        else // Cold summer
                        {
                            koppen[pos + i] = KGClimateZones.Dsc;
                        }
                    }
                    else if (Pwdry[i] >= 0 && Pwdry[i] < (Pswet[i] / 10)) // Dry winter 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dwa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dwb;
                        }
                        else if (Tcold[i] < -38) // Very cold winter 
                        {
                            koppen[pos + i] = KGClimateZones.Dwd;
                        }
                        else // Cold summer
                        {
                            koppen[pos + i] = KGClimateZones.Dwc;
                        }
                    }
                    else // Without dry season 
                    {
                        if (Thot[i] >= 22) // Hot summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dfa;
                        }
                        else if (Tmon10[i] >= 4) // Warm summer 
                        {
                            koppen[pos + i] = KGClimateZones.Dfb;
                        }
                        else if (Tcold[i] < -38) // Very cold winter  
                        {
                            koppen[pos + i] = KGClimateZones.Dfd;
                        }
                        else // Cold summer
                        {
                            koppen[pos + i] = KGClimateZones.Dfc;
                        }
                    }
                }
                else if (Thot[i] <= 10) // Polar 
                {
                    if (Thot[i] > 0) // Tundra
                    {
                        koppen[pos + i] = KGClimateZones.T;
                    }
                    else // Frost
                    {
                        koppen[pos + i] = KGClimateZones.F;
                    }

                }
                else
                {
                    Console.WriteLine("Error: Root " + i + " " + pos);
                }
            }

        }

        class KGClimateZones
        {
            // Tropical Not (B) & Tcold≥18 
            public static short Af = 1; // Color.Blue.ToArgb(); // - Rainforest Pdry≥60
            public static short Am = 2; //Color.DodgerBlue.ToArgb(); // - Monsoon Not (Af) & Pdry≥100-MAP/25 
            public static short Aw = 3; //Color.DeepSkyBlue.ToArgb(); // - Savannah Not (Af) & Pdry<100-MAP/25
            // Arid MAP<10×Pthreshold
            public static short BWh = 4; //Color.Red.ToArgb(); // - Desert Hot MAP<5×Pthreshold MAT≥18 
            public static short BWk = 5; //Color.Salmon.ToArgb(); // - Desert Cold MAP<5×Pthreshold MAT<18 
            public static short BSh = 6; //Color.Orange.ToArgb(); // - Steppe Hot MAP≥5×Pthreshold MAT≥18 
            public static short BSk = 7; //Color.PeachPuff.ToArgb(); // - Steppe  Cold MAP≥5×Pthreshold MAT<18 
            // Temperate  Not (B) & Thot>10 & 0<Tcold<18 
            public static short Csa = 8; //Color.Yellow.ToArgb(); // - Dry summer Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Csb = 9; //Color.DarkKhaki.ToArgb(); // - Dry summer Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Csc = 10; //Color.Olive.ToArgb(); // - Dry summer Cold summer Not (Cs) or (Cw) Not (a or b) & 1≤Tmon10<4 
            public static short Cwa = 11; //Color.Aquamarine.ToArgb(); // - Dry winter Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Cwb = 12; //Color.MediumSeaGreen.ToArgb(); // - Dry winter Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Cwc = 13; //Color.DarkGreen.ToArgb(); // - Dry winter Cold summer Not (Cs) or (Cw) Not (a or b) & 1≤Tmon10<4 
            public static short Cfa = 14; //Color.PaleGreen.ToArgb(); // - Without dry season Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Cfb = 15; //Color.GreenYellow.ToArgb(); // - Without dry season Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Cfc = 16; //Color.SeaGreen.ToArgb(); // - Without dry season Cold summer Not (Cs) or (Cw) Not (a or b) & 1≤Tmon10<4 
            // Cold  Not (B) & Thot>10 & Tcold≤0 
            public static short Dsa = 17; //Color.Magenta.ToArgb(); // - Dry summer Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Dsb = 18; //Color.DarkMagenta.ToArgb(); // - Dry summer Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Dsc = 19; //Color.MediumOrchid.ToArgb(); // - Dry summer Cold summer Not (Ds) or (Dw) Not (a, b, or d) 
            public static short Dsd = 20; //Color.BlueViolet.ToArgb(); // - Dry summer Very cold winter Not (Ds) or (Dw) Not (a or b) & 1≤Tmon10<4 
            public static short Dwa = 21; //Color.LightSteelBlue.ToArgb(); // - Dry winter Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Dwb = 22; //Color.RoyalBlue.ToArgb(); // - Dry winter Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Dwc = 23; //Color.MediumSlateBlue.ToArgb(); // - Dry winter Cold summer Not (Ds) or (Dw) Not (a, b, or d)  
            public static short Dwd = 24; //Color.Indigo.ToArgb(); // - Dry winter Very cold winter Not (Ds) or (Dw) Not (a or b) & Tcold<-38 
            public static short Dfa = 25; //Color.Cyan.ToArgb(); // - Without dry season Hot summer Psdry<40 & Psdry<Pwwet/3 Thot≥22 
            public static short Dfb = 26; //Color.MediumTurquoise.ToArgb(); // - Without dry season Warm summer Pwdry<Pswet/10 Not (a) & Tmon10≥4 
            public static short Dfc = 27; //Color.LightSeaGreen.ToArgb(); // - Without dry season Cold summer Not (Ds) or (Dw) Not (a, b, or d) 
            public static short Dfd = 28; //Color.DarkSlateGray.ToArgb(); // - Without dry season Very cold winter Not (Ds) or (Dw) Not (a or b) & Tcold<-38 
            // Polar  Not (B) & Thot≤10 
            public static short T = 29; //Color.LightGray.ToArgb(); // - Tundra Thot>0 
            public static short F = 30; //Color.Gray.ToArgb(); // - Frost Thot≤0 
        }

        string getFileNameTemp(string tmeansFileNames, int month)
        {
            if (PMIP3_CNRM_CM5 || PMIP3_MPI_ESM_P)
            {
                return tmeansFileNames + month + "_1.tif";
            }
            else
            {
                return tmeansFileNames + (month < 10 ? "0" : "") + month + "_1979-2013_V1.2_land.tif";
            }
        }

        private string getFileNamePrec(string precsFileNames, int month)
        {
            if (PMIP3_CNRM_CM5 || PMIP3_MPI_ESM_P)
            {
                return precsFileNames + month + "_1.tif";
            }
            else
            {
                return precsFileNames + (month < 10 ? "0" : "") + month + "_V1.2_land.tif";
            }
        }

    int convertTemp(int temp)
        {
            if (PMIP3_CNRM_CM5 || PMIP3_MPI_ESM_P) // Temperatures in Kelvin/10
            {
                return temp / 10 - 273; ;
            }
            else // CHELSA current
            {
                return temp / 10;
            }
        }

        short convertPrec(short prec)
        {
            if (PMIP3_CNRM_CM5 || PMIP3_MPI_ESM_P) // Precipitation in mm/10
            {
                return (short)(prec / 10);
            }
            else // CHELSA current
            {
                return prec;
            }
        }

    }
}

