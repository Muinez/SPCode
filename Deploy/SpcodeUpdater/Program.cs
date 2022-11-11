﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace SPCodeUpdater
{
    public static class Program
    {
        private static bool Success;
        [STAThread]
        public static void Main()
        {
            var processes = Process.GetProcessesByName("SPCode");
            foreach (var process in processes)
            {
                try
                {
                    process.WaitForExit();
                }
                catch (Exception)
                {

                }
            }

            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Thread.Sleep(2000); // why?
            Application.SetCompatibleTextRenderingDefault(true);
            var um = new UpdateMarquee();
            um.Show();
            Application.DoEvents(); //execute Visual
            var t = new Thread(Worker);
            t.Start(um);
            Application.Run(um);
        }

        private static void Worker(object arg)
        {
            try
            {
                var um = (UpdateMarquee)arg;

#if BETA
                var zipFile = ".\\SPCode.Beta.Portable.zip";
#else
                var zipFile = ".\\SPCode.Portable.zip";
#endif

                using (var fsInput = File.OpenRead(zipFile))
                using (var zf = new ZipFile(fsInput))
                {

                    foreach (ZipEntry zipEntry in zf)
                    {
                        if (zipEntry.Name.Contains("sourcepawn\\"))
                        {
                            continue;
                        }

                        var entryFileName = zipEntry.Name;

                        var fullZipToPath = Path.Combine(@".\", entryFileName);
                        var directoryName = Path.GetDirectoryName(fullZipToPath);

                        if (directoryName.Length > 0)
                        {
                            Directory.CreateDirectory(directoryName);
                        }

                        var buffer = new byte[4096];

                        using (var zipStream = zf.GetInputStream(zipEntry))
                        using (Stream fsOutput = File.Create(fullZipToPath))
                        {
                            StreamUtils.Copy(zipStream, fsOutput, buffer);
                        }
                    }
                }
                Success = true;
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("The updater failed to update SPCode properly.");
                sb.AppendLine("=============================================");
                sb.AppendLine($"Exception message: {ex.Message}");
                sb.AppendLine("=============================================");
                sb.AppendLine($"Stack trace:\n{ex.StackTrace}");
                sb.AppendLine("=============================================");
                var thread = new Thread(() => MessageBox.Show(sb.ToString()));
                thread.Start();
                Success = false;
            }
            finally
            {
                Process.Start(new ProcessStartInfo
                {
                    Arguments = $"/C SPCode.exe {(Success ? "--updateok" : "--updatefail")}",
                    FileName = "cmd",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                Application.Exit();
            }
        }
    }
}
