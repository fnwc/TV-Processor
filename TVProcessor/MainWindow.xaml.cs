using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Configuration;
using System.Globalization;
using Parago.Windows;
using TVProcessor.OptionsDIalog;

namespace TVProcessor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool _isReadOnly = false; // for debugging
        public bool _isProcessRunning = false;
        public DirectoryInfo _downloadingDirectory; // current working download directory
        public DirectoryInfo _processingDirectory; // current working processing directory
        public DirectoryInfo _downloadingFolder = new DirectoryInfo(Properties.Settings.Default.DownloadingFolderRequiredPrefix);
        public DirectoryInfo _processingFolder = new DirectoryInfo(Properties.Settings.Default.ProcessingFolderRequiredPrefix);
        public Dictionary<TargetMediaType, DirectoryInfo> _targetFolders = new Dictionary<TargetMediaType, DirectoryInfo>();
        public CultureInfo _currentCulture = new CultureInfo("en-US");
        public string[] _extensionsToCopy = new string[] { ".mp4", ".mpeg", ".mpg", ".avi", ".ts", ".mkv", ".flv" }; // lower case only
        public List<KeyValuePair<OutputType, string>> _currentMessages = new List<KeyValuePair<OutputType, string>>();
        public List<KeyValuePair<OutputType, string>> _currentErrors = new List<KeyValuePair<OutputType, string>>();
        public Dictionary<string, int> _processedShows = new Dictionary<string, int>();

        public enum OutputType { Normal, Error }
        public enum TargetMediaType
        {
            TV,
            TVDocumentary,
        }

        public MainWindow()
        {
            InitializeComponent();

            // setup events
            DirectoryDropZone.DragEnter += DirectoryDropZone_DragEnter;
            DirectoryDropZone.Drop += DirectoryDropZone_Drop;

            // handle target folders
            _targetFolders.Add(TargetMediaType.TV, new DirectoryInfo(Properties.Settings.Default.TVShowFolderRequiredPrefix));
            _targetFolders.Add(TargetMediaType.TVDocumentary, new DirectoryInfo(Properties.Settings.Default.TVDocumentaryShowFolderRequiredPrefix));

            // validate folders
            foreach (var folder in new DirectoryInfo[] { _downloadingFolder, _processingFolder, _targetFolders[TargetMediaType.TV], _targetFolders[TargetMediaType.TVDocumentary] })
            {
                if (!folder.Exists)
                {
                    MessageBox.Show($"The default folder \"{folder.FullName}\" does not exist or is not accessible. Please check your application configuration settings.", "Invalid Directory");
                    SetFormState(false);
                }
            }
        }

        public void ProcessDirectories()
        {
            try
            {
                if (_downloadingDirectory != null && _downloadingDirectory.Exists && _processingDirectory != null && _processingDirectory.Exists)
                {
                    // first get files to move that are nonzero in size (downloading to processing)
                    foreach (var file in _downloadingDirectory.GetFiles("*", SearchOption.AllDirectories).Where(o => _extensionsToCopy.Contains(o.Extension.ToLower()) && o.Length > 0 && o.Exists))
                    {
                        // look for Season folder in processing directory, if it doesn't exist, create
                        if (file.Directory.Name.StartsWith("Season"))
                        {
                            var processingDirectorySeasonFolder = _processingDirectory.GetDirectories(file.Directory.Name).SingleOrDefault();
                            if (processingDirectorySeasonFolder == null)
                                processingDirectorySeasonFolder = _processingDirectory.CreateSubdirectory(file.Directory.Name);

                            // verify season folder exists
                            if (processingDirectorySeasonFolder != null)
                            {
                                if (!_isReadOnly)
                                {
                                    // move file
                                    file.MoveTo(System.IO.Path.Combine(processingDirectorySeasonFolder.FullName, file.Name.Replace(' ', '.')));
                                    WriteMessage($"Moved file to {file.FullName}");

                                    // increment count
                                    if (_processedShows.ContainsKey(_downloadingDirectory.Name)) _processedShows[_downloadingDirectory.Name]++;
                                    else _processedShows.Add(_downloadingDirectory.Name, 1);
                                }
                                else
                                    WriteMessage($"Could not move read-only file {file.FullName}", OutputType.Error);
                            }
                        }
                    }

                    // test directory to make sure we need to mirror back
                    if (_processingDirectory.GetFiles("*", SearchOption.AllDirectories).Where(o => _extensionsToCopy.Contains(o.Extension.ToLower()) && o.Length > 0 && o.Exists).Any())
                    {
                        // use ROBOCOPY to do actual mirroring
                        int timeout = 30000;
                        var output = new StringBuilder();
                        var error = new StringBuilder();
                        string args = String.Format(@"""{0}"" ""{1}"" {2} /CREATE /E /XD .actors", _processingDirectory.FullName, _downloadingDirectory.FullName, String.Join(" ", _extensionsToCopy.Select(o => "*" + o)));
                        if (!_isReadOnly)
                        {
                            var p = new Process()
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = @"C:\Windows\System32\robocopy.exe",
                                    Arguments = args,
                                    RedirectStandardError = true,
                                    RedirectStandardOutput = true,
                                    WindowStyle = ProcessWindowStyle.Normal,
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardInput = true,
                                }
                            };
                            using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                            using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                            {
                                // output events
                                p.OutputDataReceived += (s, evt) =>
                                {
                                    if (evt.Data == null)
                                    {
                                        outputWaitHandle.Set();
                                    }
                                    else
                                    {
                                        output.AppendLine(evt.Data);

                                    }
                                };
                                p.ErrorDataReceived += (s, evt) =>
                                {
                                    if (evt.Data == null)
                                    {
                                        errorWaitHandle.Set();
                                    }
                                    else
                                    {
                                        output.AppendLine(evt.Data);
                                    }
                                };

                                // start process
                                p.Start();
                                p.BeginOutputReadLine();
                                p.BeginErrorReadLine();

                                if (p.WaitForExit(timeout) &&
                                    outputWaitHandle.WaitOne(timeout) &&
                                    errorWaitHandle.WaitOne(timeout))
                                {
                                    // Process completed. Check process.ExitCode here.
                                }
                                else
                                {
                                    // Timed out.
                                }
                            }

                            // handle output
                            WriteMessage(output.ToString());
                            WriteMessage(error.ToString(), OutputType.Error);

                            // special: delete .actors folder in target
                            foreach (var deleteDir in _downloadingDirectory.GetDirectories(".actors", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    deleteDir.Delete(true);
                                }
                                catch (Exception e)
                                {
                                    WriteMessage($"Can't delete folder {deleteDir.FullName}: {e.Message}", OutputType.Error);
                                }
                            }
                        }
                        else
                        {
                            // debug show what robocopy command would have been run
                            WriteMessage(args);
                        }
                    }
                }
                else
                    WriteMessage("Please drop a valid subdirectory to begin.", OutputType.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The exception occurred: " + ex.Message, "Exception Occurred");
            }
        }

        public void DirectoryDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filePaths = (string[])(e.Data.GetData(DataFormats.FileDrop));
                if (filePaths.Any())
                {
                    if (filePaths.Length == 1)
                    {
                        var dir = new DirectoryInfo(filePaths[0]);
                        if (dir.Exists)
                        {
                            if (dir.FullName.StartsWith(_processingFolder.FullName, true, _currentCulture))
                            {
                                // check if dir is a sub of processing folder, and match accordingly
                                MatchDirectories(dir, _processingFolder, out _processingDirectory, _downloadingFolder, out _downloadingDirectory);
                            }
                            else if (dir.FullName.StartsWith(_downloadingFolder.FullName, true, _currentCulture))
                            {
                                // check if dir is a sub of downloading folder, and match accordingly
                                MatchDirectories(dir, _downloadingFolder, out _downloadingDirectory, _processingFolder, out _processingDirectory);
                            }
                        }
                    }
                }
            }

            // invalid
            if (_downloadingDirectory == null || _processingDirectory == null)
            {
                WriteMessage("The directory must be a valid subdirectory of either the Default Source Folder or Default Target Folder.", OutputType.Error);
                SetFormState(false);
            }
            else
            {
                SetFormState(true);

                // clear output & process
                ClearMessages();

                // show progress handler
                var result = ProgressDialog.Execute(this, "Running Script...", (bw, we) =>
                {
                    // process
                    ProcessDirectories();
                });

                // show results
                if (!result.OperationFailed) ShowPostProcessMessages();
            }
        }

        public void MatchDirectories(DirectoryInfo d, DirectoryInfo default1, out DirectoryInfo directoryToMatch1, DirectoryInfo default2, out DirectoryInfo directoryToMatch2)
        {
            // reset
            directoryToMatch1 = null;
            directoryToMatch2 = null;

            // get the sub of the "source" default folder
            var sub1 = default1.GetDirectories(d.Name).SingleOrDefault();

            // match the sub in the "target" default folder
            var sub2 = default2.GetDirectories(d.Name).SingleOrDefault();
            if (sub1 != null && sub1.Exists && sub2 != null && sub2.Exists)
            {
                directoryToMatch1 = sub1;
                directoryToMatch2 = sub2;
            }
            else if (default1 != _processingFolder && default2 != _processingFolder)
            {
                // failed to find matching diretories
                WriteMessage(String.Format("Unable to find matching directories, from \"{0}{2}\"  to \"{1}{2}\".", default1.FullName, default2.FullName, d.Name), OutputType.Error);
            }
        }

        public void DirectoryDropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        public void WriteMessage(string s)
        {
            WriteMessage(s, OutputType.Normal);
        }

        public void WriteMessage(string s, OutputType outputType)
        {
            if (!String.IsNullOrWhiteSpace(s))
            {
                // handle readonly
                if (_isReadOnly) s = "READONLY: " + s;

                // handle which list
                switch (outputType)
                {
                    case OutputType.Error:
                        _currentErrors.Add(new KeyValuePair<OutputType, string>(outputType, s));
                        break;

                    default:
                        _currentMessages.Add(new KeyValuePair<OutputType, string>(outputType, s));
                        break;
                }
            }
        }

        public void FlushMessages(ScrollViewer viewer, List<KeyValuePair<OutputType, string>> messages)
        {
            foreach (var message in messages)
            {
                // text
                var text = new TextBlock { Text = message.Value };

                // handle font styling
                switch (message.Key)
                {
                    case OutputType.Error:
                        viewer = Error;
                        text.Foreground = Brushes.Red;
                        break;

                    default:
                        text.Foreground = Brushes.White;
                        break;
                }

                // add
                ((StackPanel)viewer.Content).Children.Add(text);

                // scroll
                viewer.ScrollToBottom();
            }

            // clear structure
            messages.Clear();
        }

        public void ClearMessages()
        {
            ((StackPanel)Output.Content).Children.Clear();
            ((StackPanel)Error.Content).Children.Clear();
        }

        private void SetFormState(bool enabled)
        {
            DirectoryDropZone.IsEnabled = enabled;
            RunEntireFolder.IsEnabled = enabled;
        }

        private void DownloadingFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadingFolder != null) Process.Start(_downloadingFolder.FullName);
        }

        private void ProcessingFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_processingFolder != null) Process.Start(_processingFolder.FullName);
        }

        private void RunEntireFolder_Click(object sender, RoutedEventArgs e)
        {
            // clear output 
            ClearMessages();

            // show progress handler
            var result = ProgressDialog.Execute(this, "Running Script...", (bw, we) =>
            {
                // tracking for progress
                int count = 0;
                var dirs = _downloadingFolder.GetDirectories();

                // process all downloading folders (but not processing directory)
                foreach (var d in dirs)
                {
                    // increment count
                    count++;

                    // show progress
                    ProgressDialog.Report(bw, $"Matching {d.FullName}");

                    // match subdirectories
                    MatchDirectories(d, _downloadingFolder, out _downloadingDirectory, _processingFolder, out _processingDirectory);

                    // if we matched, process
                    if (_downloadingDirectory != null && _processingDirectory != null)
                    {
                        int pct = Convert.ToInt32((Convert.ToDecimal(count) / Convert.ToDecimal(dirs.Count())) * 100.0m);

                        // show progress
                        if (ProgressDialog.ReportWithCancellationCheck(bw, we, pct, $"Processing {d.FullName}")) return;

                        // avoid default processing after the Cancel button has been pressed.
                        // This call will set the Cancelled flag on the result structure.
                        ProgressDialog.CheckForPendingCancellation(bw, we);

                        //process
                        ProcessDirectories();
                    }
                }

            }, new ProgressDialogSettings(true, true, false));

            // show results
            if (!result.OperationFailed) ShowPostProcessMessages();
        }

        private void ShowPostProcessMessages()
        {
            // flush output
            FlushMessages(Output, _currentMessages);
            FlushMessages(Error, _currentErrors);

            // flush processed shows
            var sb = new System.Text.StringBuilder();
            foreach (var show in _processedShows)
            {
                sb.Append($"{show.Key}: {show.Value} episodes\n");
            }
            if (sb.Length > 0) MessageBox.Show("Processed:\n============\n\n" + sb.ToString());
            _processedShows.Clear();
        }

        private void File_Options_Click(object sender, RoutedEventArgs e)
        {
            var options = new OptionsDialog() { Owner = this };
            options.Show();
        }

        private void File_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
