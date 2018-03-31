using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace KombinerBillederFilm
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void chooseFolder_Click(object sender, EventArgs e)
        {
            string initialDir = textBoxFilesDir.Text;
            folderBrowserDialog.SelectedPath = initialDir;     
            DialogResult result = folderBrowserDialog.ShowDialog();
            if(result == DialogResult.OK)
            {
                initialDir = folderBrowserDialog.SelectedPath;
                Console.WriteLine("Chose directory: " + initialDir);
                textBoxFilesDir.Text = initialDir;
                textBoxFilesDir.Update();
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            string baseDir = textBoxFilesDir.Text;

            if (textBoxDescription.Text == null || "".Equals(textBoxDescription.Text)||baseDir == null || "".Equals(baseDir))
            {
                MessageBox.Show("Der skal angives en beskrivelse og sti til filer", "Mangler information",MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var subDirs = Directory.EnumerateDirectories(baseDir);
            IEnumerable<string> jpegFiles = null;
            IEnumerable<string> mtsFiles = null;
            foreach (string subdir in subDirs)
            {
                try
                {
                    if (jpegFiles == null)
                    {
                        var jpeg = Directory.EnumerateFileSystemEntries(subdir, "*.JPG", SearchOption.AllDirectories);
                        if (jpeg.ToList<string>().LongCount() > 0)
                        {
                            jpegFiles = jpeg;
                        }
                    }
                    if (mtsFiles == null)
                    {
                        var mts = Directory.EnumerateFileSystemEntries(subdir, "*.MTS", SearchOption.AllDirectories);
                        if (mts.ToList<string>().LongCount() > 0)
                        {
                            mtsFiles = mts;
                        }
                    }
                } catch(System.UnauthorizedAccessException exception)
                {
                    Console.Error.WriteLineAsync(exception.Message);
                }
                if (jpegFiles != null && mtsFiles != null) break;
            }
            progressPictures.Maximum = jpegFiles.Count()+ mtsFiles.Count();

            string tempDir = Environment.GetEnvironmentVariable("TEMP");
            string uuid = System.Guid.NewGuid().ToString();
            DirectoryInfo workDir = Directory.CreateDirectory(tempDir + "\\KombinerBillederFilm\\" + uuid);

            PictureHandler pictureHandler = new PictureHandler();
            pictureHandler.SetProgressBar(progressPictures);
            pictureHandler.SetWorkDir(workDir);
            pictureHandler.Convert(jpegFiles); 

            MovieHandler movieHandler = new MovieHandler();
            movieHandler.setProgressBar(progressPictures);
            movieHandler.SetWorkDir(workDir);
            movieHandler.Convert(mtsFiles);

            SortedList<DateTime, string> story = new SortedList<DateTime, string>();
            CreateStory(story, jpegFiles);
            CreateStory(story, mtsFiles);
            FileInfo result = movieHandler.assemble(story, textBoxDescription.Text);

            if (result.Exists)
            {
                File.Move(result.FullName, Environment.GetEnvironmentVariable("USERPROFILE") + "\\Videos\\" + result.Name);
                workDir.Delete(true);
                buttonStart.Text = "Færdig";
                buttonStart.Enabled = false;
            }
        }

        private void CreateStory(SortedList<DateTime, string> story, IEnumerable<string> files)
        {
            SortedList<DateTime, SortedList<string,string>> internalStory = new SortedList<DateTime, SortedList<string,string>>();

            foreach (string f in files)
            {
                FileInfo original = new FileInfo(f);
                SortedList<string,string> concurrentFiles = null;
                DateTime creation = original.CreationTime;
                if (internalStory.ContainsKey(creation))
                {
                    concurrentFiles = internalStory[creation];
                }
                else
                {
                    concurrentFiles = new SortedList<string, string>();
                    internalStory.Add(creation,concurrentFiles);
                }
                concurrentFiles.Add(original.Name, original.Name);
            }
            foreach(DateTime creation in internalStory.Keys)
            {
                SortedList<string, string> concurrentFiles = internalStory[creation];
                DateTime dt = creation;
                foreach(string s in concurrentFiles.Keys)
                {
                    story.Add(dt, s);
                    dt = dt.AddMilliseconds(1);
                }
            }
        }
    }
}
