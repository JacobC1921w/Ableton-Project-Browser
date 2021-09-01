using Microsoft.Win32;
using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Diagnostics;

namespace Ableton_Project_Browser {
    public partial class MainForm : Form {

        /// <summary>
        ///  Make defaultProjectLocation a global variable
        /// </summary>
        public string defaultProjectLocation { get; private set; }
        public MainForm() => InitializeComponent();

        /// <summary>
        ///   A method for opening the gzip .als project file, "parsing" the XML and finding the song temp. By far the worst piece of code on my whole repository
        /// </summary>
        /// <param name="file">The full location of the .als file.</param>
        string getTempoFromALSFile(string file) {
            using (FileStream fileReader = File.OpenRead(file)) {
                using(GZipStream gzip = new(fileReader, CompressionMode.Decompress, true)) {
                    using(StreamReader ungzip = new(gzip)) {
                        string extractedText = ungzip.ReadToEnd();
                        using (StringReader stringReader = new(extractedText.Substring(extractedText.IndexOf("<Tempo>") + "<Tempo>".Length, extractedText.LastIndexOf("</Tempo>") - extractedText.IndexOf("<Tempo>") + "<Tempo>".Length))) {
                            string line = string.Empty;
                            do {
                                line = stringReader.ReadLine();
                                if(line != null) {
                                    if(line.Contains("<Manual Value=\"")) {
                                        return line.Split("\"")[1];
                                    }
                                }
                            } while(line != null);
                        }
                    }
                }
            }
            return null;
        }

        private void MainForm_Load(object sender, EventArgs e) {
            ///
            /// Disable the name and BPM columns, I don't want you to edit this :p
            ///
            mainDataGrid.Columns["nameColumn"].ReadOnly = true;
            mainDataGrid.Columns["BPMColumn"].ReadOnly = true;

            ///
            /// Registry checking stuff
            ///
            if (Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB") == null) {
                Registry.CurrentUser.CreateSubKey(@"SOFTWARE\APB");
            }

            ///
            /// Create a sub-key for the default project directory
            ///
            if (Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB").GetValue("defaultProjectPath") == null) {
                Registry.CurrentUser.CreateSubKey(@"SOFTWARE\APB").SetValue("defaultProjectPath", Interaction.InputBox("Default Project Location", "Default Project Location"));
            }


            ///
            /// Iterate over projects in the path, making sure its a valid project directory
            ///
            foreach (string directory in Directory.EnumerateDirectories(Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB").GetValue("defaultProjectPath").ToString())) {
                if(Directory.GetFiles(directory, "*.als", SearchOption.TopDirectoryOnly).Length > 0) {
                    ///
                    /// Create a config file if it doesn't exist already
                    ///
                    if (!File.Exists(directory + @"\APB.yaml")) {
                        File.WriteAllLines(directory + @"\APB.yaml", new string[] { "Name: " + directory.Split(@"\").Last().Replace(" Project", ""), "Key: ", "BPM: " + getTempoFromALSFile(directory + @"\" + directory.Split(@"\").Last().Replace(" Project", "") + ".als"), "Description: " });
                    }


                    ///
                    /// Start setting up the datagridview
                    ///
                    int index = mainDataGrid.Rows.Add();

                    mainDataGrid.Rows[index].Cells["nameColumn"].Value = "";
                    mainDataGrid.Rows[index].Cells["keyColumn"].Value = "";
                    mainDataGrid.Rows[index].Cells["BPMColumn"].Value = "";
                    mainDataGrid.Rows[index].Cells["descriptionColumn"].Value = "";
                    mainDataGrid.Rows[index].Cells["locationColumn"].Value = directory;

                    foreach (string line in File.ReadAllLines(directory + @"\APB.yaml")) {
                        if(line.StartsWith("Name: ")) {
                            mainDataGrid.Rows[index].Cells["nameColumn"].Value = line[6..];
                        } else if (line.StartsWith("Key: ")) {
                            mainDataGrid.Rows[index].Cells["keyColumn"].Value = line[5..];
                        } else if (line.StartsWith("BPM: ")) {
                            mainDataGrid.Rows[index].Cells["BPMColumn"].Value = line[5..];
                        } else if (line.StartsWith("Description: ")) {
                            mainDataGrid.Rows[index].Cells["descriptionColumn"].Value = line[13..];
                        }
                    }
                }
            }

        }

        /// <summary>
        /// When doubleclicking the main form, allow the default project location to be changed. TODO: refresh main form
        /// </summary>
        private void MainForm_DoubleClick(object sender, EventArgs e) {
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\APB").SetValue("defaultProjectPath", Interaction.InputBox("Default Project Location", "Default Project Location", Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB").GetValue("defaultProjectPath").ToString()));
            defaultProjectLocation = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB").GetValue("defaultProjectPath").ToString();
        }

        /// <summary>
        /// Update project config file based on edited data
        /// </summary>
        private void mainDataGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            File.WriteAllLines(mainDataGrid.Rows[e.RowIndex].Cells["locationColumn"].Value + @"\APB.yaml", new string[] { "Name: " + mainDataGrid.Rows[e.RowIndex].Cells["nameColumn"].Value, "Key: " + mainDataGrid.Rows[e.RowIndex].Cells["keyColumn"].Value, "BPM: " + mainDataGrid.Rows[e.RowIndex].Cells["BPMColumn"].Value, "Description: " + mainDataGrid.Rows[e.RowIndex].Cells["descriptionColumn"].Value });
        }

        /// <summary>
        /// Make row headers doubleclickable, and open ableton with the project.
        /// </summary>
        private void mainDataGrid_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e) {
            Process proc = new();
            proc.StartInfo.FileName = Registry.ClassesRoot.OpenSubKey(@"Ableton.Live.AppLiveSuite.als.10\shell\Open\Command").GetValue("").ToString().Replace("\"%1\"", "");
            proc.StartInfo.Arguments = "\"" + Registry.CurrentUser.OpenSubKey(@"SOFTWARE\APB").GetValue("defaultProjectPath").ToString() + @"\" + mainDataGrid.Rows[e.RowIndex].Cells["nameColumn"].Value + @" Project\" + mainDataGrid.Rows[e.RowIndex].Cells["nameColumn"].Value + ".als\"";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
        }
    }
}
