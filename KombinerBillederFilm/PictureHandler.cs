using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImageMagick;


namespace KombinerBillederFilm
{
    class PictureHandler
    {
        private const int nFades = 12;
        private const int maxTasks = 4;
        private const double W16H9 = 16.0 / 9.0;

        private int loopLength = 4;
        private MagickGeometry h1080 = new MagickGeometry(1920, 1080);
        private MagickColor blackColor = new ColorMono(true);
        private DirectoryInfo workDir = null;
        private DirectoryInfo pictDir = null;
        private DirectoryInfo moviesDir = null;
        private string ffmpeg = null;
        private ProgressBar progressBar = null;

        internal void Convert(IEnumerable<string> jpegFiles)
        {
            Initialize();

            Task[] tasks = new Task[maxTasks];
            int taskIx = 0;

            try
            {
                foreach (string jpeg in jpegFiles)
                {
                    IncrementProgress();
                    if (tasks[taskIx] != null)
                    {
                        WaitForAndDisposeTask(tasks[taskIx]);
                    }
                    tasks[taskIx] = Task.Run(() => { ConvertJpeg(jpeg); });
                    taskIx++;
                    if (taskIx >= maxTasks) { taskIx = 0; }
                }
            }
            catch (Exception anyException)
            {
                Console.Error.WriteLineAsync(anyException.Message);
            }
            finally
            {
                foreach(Task t in tasks)
                {
                    WaitForAndDisposeTask(t);
                }
            }
        }

        private static void WaitForAndDisposeTask(Task t)
        {
            if (!t.IsCompleted)
            {
                try { t.Wait(); } catch (AggregateException e) { Console.Error.WriteLine(e.Message); }
            }
            t.Dispose();
        }

        private void ConvertJpeg(string jpeg)
        {
            try
            {
                using (IMagickImage magickImage = new MagickImage(jpeg))
                {
                    OrientationType orientation = magickImage.Orientation;
                    MagickGeometry geometry = h1080;
                    geometry.IgnoreAspectRatio = false;
                    if (magickImage.Orientation == OrientationType.LeftBotom)
                    {
                        magickImage.Rotate(-90.0);
                    }
                    magickImage.Resize(geometry);
                    IMagickImage result = null;
                    result = FillImage(magickImage);

                    FileInfo original = new FileInfo(jpeg);
                    string filename = String.Format("{0}_{1,2:00}.JPG", original.Name.Substring(0, (original.Name.LastIndexOf("."))), 0);
                    FileInfo converted = new FileInfo(pictDir.FullName + "\\" + filename);
                    result.Write(converted);
                    FadeImage(pictDir, result, original);
                    result.Dispose();

                    ToMpeg(converted);
                }
            }
            catch (ImageMagick.MagickCorruptImageErrorException imgErrorException)
            {
                Console.Error.WriteLineAsync(imgErrorException.Message);
            }
        }

        internal void SetProgressBar(ProgressBar progressBar)
        {
            this.progressBar = progressBar;
        }

        private void IncrementProgress()
        {
            if(progressBar != null)
            {
                progressBar.Increment(1);
                //progressBar.Update();
            }
        }

        private void ToMpeg(FileInfo converted)
        {
            string pict = converted.Name.Substring(0, converted.Name.LastIndexOf("_"));
            fade(pict, "_2.MPG", 1, true);

            using (Process pictLoop = new Process())
            {
                pictLoop.StartInfo.FileName = ffmpeg;
                pictLoop.StartInfo.Arguments = "-y -t " + loopLength + " -loop 1 -i "
                    + converted.FullName + " -i " + workDir.FullName + "\\silence.ac3 -map 0:0 -map 1:0 -c:a:1 copy -c:v:0 mpeg2video -r 25 -pix_fmt yuv420p -q:v 2 -target pal-dvd -shortest "
                    + moviesDir.FullName + "\\" + pict + "_1.MPG";
                pictLoop.StartInfo.CreateNoWindow = true;
                pictLoop.StartInfo.UseShellExecute = false;
                pictLoop.Start();
            }

            // Rename/re-order fades
            for (int i = 0; i < nFades; i++)
            {
                File.Move(pictDir.FullName + "\\" + String.Format("{0}_{1}.JPG", pict, (i + 1)), pictDir.FullName + "\\" + String.Format("{0}_{1}.JPG", pict, (2 * nFades - i - 1)));
            }

            fade(pict, "_0.MPG", 12, false);
        }

        private void fade(string pict, string postFix, int startNumber, Boolean waitForExit)
        {
            using (Process fadeProcess = new Process())
            {
                fadeProcess.StartInfo.FileName = ffmpeg;
                fadeProcess.StartInfo.Arguments = "-y -framerate 25 -start_number " + startNumber
                    + " -i " + pictDir.FullName + "\\" + pict + "_%d.JPG -i "
                    + workDir.FullName + "\\silence.ac3 -map 0:0 -map 1:0 -c:a:1 copy -c:v:0 mpeg2video -r 25 -pix_fmt yuv420p -q:v 2 -target pal-dvd -shortest "
                    + moviesDir.FullName + "\\" + pict + postFix;
                fadeProcess.StartInfo.CreateNoWindow = true;
                fadeProcess.StartInfo.UseShellExecute = false;
                fadeProcess.Start();
                if (waitForExit)
                {
                    fadeProcess.WaitForExit();
                }
            }
        }

        private void FadeImage(DirectoryInfo pictDir, IMagickImage result, FileInfo original)
        {
            Percentage p = new Percentage(12.0);
            for (int i = 0; i < nFades; i++)
            {
                result.Colorize(blackColor, p);
                string filename = String.Format("{0}_{1}.JPG", original.Name.Substring(0, (original.Name.LastIndexOf("."))), (i + 1));
                FileInfo colorized = new FileInfo(pictDir.FullName + "\\" + filename);
                result.Write(colorized);
            }
        }

        private IMagickImage FillImage(IMagickImage magickImage)
        {
            IMagickImage result;
            using (MagickImageCollection images = new MagickImageCollection())
            {
                int blackWidth = (1920 - magickImage.Width) / 2;

                IMagickImage blackLeft = new MagickImage(blackColor, blackWidth, 1080); ;
                IMagickImage blackRight = new MagickImage(blackColor, blackWidth, 1080); ;

                images.Add(blackLeft);
                images.Add(magickImage);
                images.Add(blackRight);

                result = images.AppendHorizontally();

                blackLeft.Dispose();
                blackRight.Dispose();
            }
            result.Orientation = OrientationType.TopLeft;
            return result;
        }

        private void Initialize()
        {
            pictDir = new DirectoryInfo(workDir.FullName + "\\pictures");
            if (!pictDir.Exists)
            {
                pictDir.Create();
            }
            moviesDir = new DirectoryInfo(workDir.FullName + "\\movies");
            if (!moviesDir.Exists)
            {
                moviesDir.Create();
            }

            byte[] silence = Properties.Resources.silence;
            File.WriteAllBytes(workDir.FullName+"\\silence.ac3", silence);

            ffmpeg = Properties.Settings.Default.ffmpeg;
            loopLength = Properties.Settings.Default.loopLength;
        }

        internal void SetWorkDir(DirectoryInfo workDir)
        {
            this.workDir = workDir;
        }
    }
}
