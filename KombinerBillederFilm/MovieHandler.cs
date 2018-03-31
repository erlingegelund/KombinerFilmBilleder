using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace KombinerBillederFilm
{
    class MovieHandler
    {
        private DirectoryInfo workDir = null;
        private DirectoryInfo moviesDir = null;
        private string ffmpeg = null;
        private ProgressBar progressBar = null;

        private void Initialize()
        {
            moviesDir = new DirectoryInfo(workDir.FullName + "/movies");
            if (!moviesDir.Exists)
            {
                moviesDir.Create();
            }

            ffmpeg = Properties.Settings.Default.ffmpeg;
        }

        internal void SetWorkDir(DirectoryInfo workDir)
        {
            this.workDir = workDir;
        }

        internal void setProgressBar(ProgressBar progressBar)
        {
            this.progressBar = progressBar;
        }

        private void incrementProgress()
        {
            if (progressBar != null)
            {
                progressBar.Increment(1);
            }
        }

        internal void Convert(IEnumerable<string> mtsFiles)
        {
            Initialize();

            foreach (string mts in mtsFiles)
            {
                incrementProgress();

                FileInfo original = new FileInfo(mts);

                using (Process pass1 = new Process())
                {
                    pass1.StartInfo.FileName = ffmpeg;
                    pass1.StartInfo.Arguments = "-i " + mts + " -pass 1 -an -c:v mpeg2video -r 25 -pix_fmt yuv420p -qscale:v 2 -b:v 9000k -target pal-dvd -y NUL";
                    pass1.StartInfo.CreateNoWindow = true;
                    pass1.StartInfo.UseShellExecute = false;
                    pass1.StartInfo.WorkingDirectory = workDir.FullName;
                    pass1.Start();
                    pass1.WaitForExit();
                }

                using (Process pass2 = new Process())
                {
                    pass2.StartInfo.FileName = ffmpeg;
                    pass2.StartInfo.Arguments = "-i " + mts
                        + " -pass 2 -c:a copy -c:v mpeg2video -r 25 -pix_fmt yuv420p -qscale:v 2 -b:v 9000k -target pal-dvd -y "
                        + moviesDir.FullName + "/" + original.Name.Substring(0, original.Name.LastIndexOf(".")) + ".MPG";
                    pass2.StartInfo.CreateNoWindow = true;
                    pass2.StartInfo.UseShellExecute = false;
                    pass2.StartInfo.WorkingDirectory = workDir.FullName;
                    pass2.Start();
                    pass2.WaitForExit();
                }
            }
        }

        internal FileInfo assemble(SortedList<DateTime, string> story, string eventName)
        {
            FileInfo result = null;
            if(story.Count == 0)
            {
                return null;
            }
            StreamWriter mpgList = File.CreateText(workDir.FullName + "/mpglist.txt");
            DateTime firstDate = story.Keys[0];
            mpgList.WriteLine("#" + firstDate.ToShortDateString() + ", "+ eventName);
            foreach(string fileName in story.Values)
            {
                string noExtName = fileName.Substring(0, fileName.LastIndexOf("."));
                if (fileName.EndsWith("JPG"))
                {
                    if (File.Exists(moviesDir.FullName+"\\"+ noExtName + "_0.MPG")) { mpgList.WriteLine("file movies/" + noExtName + "_0.MPG"); }
                    if (File.Exists(moviesDir.FullName + "\\" + noExtName + "_1.MPG")) { mpgList.WriteLine("file movies/" + noExtName + "_1.MPG"); }
                    if (File.Exists(moviesDir.FullName + "\\" + noExtName + "_2.MPG")) { mpgList.WriteLine("file movies/" + noExtName + "_2.MPG"); }
                } else
                {
                    if (File.Exists(moviesDir.FullName + "\\" + noExtName + ".MPG")) { mpgList.WriteLine("file movies/" + noExtName + ".MPG"); }
                }
            }
            mpgList.Close();

            result = new FileInfo(workDir.FullName + "\\" + firstDate.Year + "-" + eventName + ".MPG");

            using (Process concat = new Process())
            {
                concat.StartInfo.FileName = ffmpeg;
                concat.StartInfo.Arguments = "-y -f concat -i mpglist.txt -c copy -target pal-dvd "+result.FullName;
                concat.StartInfo.CreateNoWindow = true;
                concat.StartInfo.UseShellExecute = false;
                concat.StartInfo.WorkingDirectory = workDir.FullName;
                concat.Start();
                concat.WaitForExit();
            }
            return result;
        }
    }
}
