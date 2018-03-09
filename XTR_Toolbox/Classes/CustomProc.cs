using System;
using System.Diagnostics;
using System.Windows;

namespace XTR_Toolbox.Classes
{
    internal static class CustomProc
    {
        public static int StartProc(string file, string arg = "", ProcessWindowStyle style = ProcessWindowStyle.Normal,
            string exMsg = "", bool wait = true)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = arg,
                WindowStyle = style
            };

            using (Process proc = new Process())
            {
                try
                {
                    proc.StartInfo = startInfo;
                    proc.Start();
                    if (wait)
                        proc.WaitForExit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{exMsg}Error: {ex.Message}");
                }

                return proc.ExitCode;
            }
        }

        public static void KillAllProc(string procName)
        {
            try
            {
                foreach (Process processKill in Process.GetProcessesByName(procName))
                    processKill.Kill();
            }
            catch
            {
                // ignored
            }
        }
    }
}