using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace GoogleMapsTiler
{
    class Program
    {
        private static string _osgeo4wBat;
        private static string _oryginalFilesPath;
        private static string _filesExtension;
        private static string _zoom;
        private static string _processes;

        private static string _formattedOutput;
        private static string _tilesOutput;
        private static bool _isFormatted;

        static void Main(string[] args)
        {
            _isFormatted = false;
            SetOsgeoBatPath();
            SetOryginalFilesPath();
            SetZoomLevel();
            SetProcessecCount();

            if (IsFormatNeeded())
            {
                _isFormatted = true;
                ShowStartInfo();
                FormatTiff();
            }
            else
            {
                ShowStartInfo();
            }

            CreateTiles();
            RemoveBlankImages();
            DeleteEmptyDirectories();
        }

        private static void ShowStartInfo()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Process started it may take a while...");
        }

        private static void SetOsgeoBatPath()
        {
            do
            {
                Console.WriteLine("Insert path to OSGeo4W.bat (e.g. c:\\OSGeo4W64\\OSGeo4W.bat)");
                _osgeo4wBat = Console.ReadLine();
                if (File.Exists(_osgeo4wBat)) break;

            } while (true);
        }

        private static void SetOryginalFilesPath()
        {

            Console.WriteLine("Insert path to files");
            _oryginalFilesPath = Console.ReadLine();
            _formattedOutput = _oryginalFilesPath;

            Console.WriteLine("Insert files extension (e.g. tif)");
            _filesExtension = "*.";
            _filesExtension += Console.ReadLine();
        }

        private static void SetZoomLevel()
        {
            Console.WriteLine("Insert zoom level (e.g. 9-18)");
            _zoom = Console.ReadLine();
        }

        private static void SetProcessecCount()
        {
            Console.WriteLine("Insert processes number (e.g. 4)");
            _processes = Console.ReadLine();
        }

        private static bool IsFormatNeeded()
        {
            Console.WriteLine("Format files to EPSG:4326? [Y] Yes [N] No");
            do
            {
                var response = Console.ReadLine();
                if (response.ToLower().Equals("y"))
                {
                    return true;
                }
                if (response.ToLower().Equals("n"))
                {
                    return false;
                }

            } while (true);
        }

        private static void FormatTiff()
        {
            _formattedOutput = Path.Combine(_oryginalFilesPath, "Formatted");

            if (!Directory.Exists(_formattedOutput))
            {
                Directory.CreateDirectory(_formattedOutput);
            }

            var files = Directory.GetFiles(_oryginalFilesPath, _filesExtension, SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var fileinfo = new FileInfo(file);
                var command = "gdalwarp -t_srs EPSG:4326 -co TILED=YES -srcnodata \"255 255 255\" -dstnodata \"255 255 255\" " +
                              file + " " + Path.Combine(_formattedOutput, fileinfo.Name);

                Process cmd = new Process();
                cmd.StartInfo.FileName = _osgeo4wBat;
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.WriteLine(command);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();
            }
        }

        private static void CreateTiles()
        {
            _tilesOutput = Path.Combine(_oryginalFilesPath, "Tiles");

            var destinationFolder = Path.Combine(_tilesOutput, "tmp.vrt");

            if (!Directory.Exists(_tilesOutput))
            {
                Directory.CreateDirectory(_tilesOutput);
            }

            var command = "gdalbuildvrt -srcnodata \"255 255 255\" -vrtnodata \"255 255 255\" " + destinationFolder + " " + Path.Combine(_formattedOutput, _filesExtension);

            Process cmd = new Process();
            cmd.StartInfo.FileName = _osgeo4wBat;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();

            Console.WriteLine(cmd.StandardOutput.ReadToEnd());
            cmd.WaitForExit();

            command = "gdal2tiles --processes=" + _processes + " --z=" + _zoom + " " + destinationFolder + " " + _tilesOutput;

            cmd = new Process();
            cmd.StartInfo.FileName = _osgeo4wBat;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();

            Console.WriteLine(cmd.StandardOutput.ReadToEnd());
            cmd.WaitForExit();

            File.Delete(destinationFolder);

            if (_isFormatted)
            {
                Directory.Delete(_formattedOutput, true);
            }
        }

        private static void RemoveBlankImages()
        {
            var files = Directory.GetFiles(_tilesOutput, "*.png", SearchOption.AllDirectories);
            var bitArray = GetEmptyPixelsList();

            foreach (var file in files)
            {
                var stream = File.OpenRead(file);
                var processedBitmap = new Bitmap(stream);


                List<bool> isOther = new List<bool>();


                unsafe
                {
                    BitmapData bitmapData = processedBitmap.LockBits(
                        new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height),
                        ImageLockMode.ReadWrite, processedBitmap.PixelFormat);

                    int bytesPerPixel = System.Drawing.Bitmap.GetPixelFormatSize(processedBitmap.PixelFormat) / 8;
                    int heightInPixels = bitmapData.Height;
                    int widthInBytes = bitmapData.Width * bytesPerPixel;
                    byte* ptrFirstPixel = (byte*)bitmapData.Scan0;


                    for (int y = 0; y < heightInPixels; y++)
                    {
                        byte* currentLine = ptrFirstPixel + (y * bitmapData.Stride);
                        for (int x = 0; x < widthInBytes; x = x + bytesPerPixel)
                        {
                            int blue = currentLine[x];
                            int green = currentLine[x + 1];
                            int red = currentLine[x + 2];
                            int alpha = currentLine[x + 3];

                            if (alpha == 0)
                            {
                                continue;
                            }

                            if (red == blue && blue == green)
                            {
                                if (bitArray.All(z => z != red))
                                {
                                    isOther.Add(true);
                                }
                            }
                            else
                            {
                                isOther.Add(true);
                            }
                        }
                    }
                    processedBitmap.UnlockBits(bitmapData);
                }
                stream.Close();

                if (!isOther.Any())
                {
                    File.Delete(file);
                }
            }
        }

        private static void DeleteEmptyDirectories()
        {
            var folders = Directory.GetDirectories(_tilesOutput, "*", SearchOption.AllDirectories);
            foreach (var dir in folders)
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }

        private static int[] GetEmptyPixelsList()
        {
            int[] array = new int[256];
            for (int i = 0; i <= 255; i++)
            {
                array[i] = i;
            }

            return array;
        }
    }
}
