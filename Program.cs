using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace ffmpegFileSizeSplit
{
    internal class Program
    {
        // args{ dir of files to split, max size in bytes, dir of output }
        static void Main(string[] args)
        {
            var files = Directory.GetFiles(args[0]);
            FileStream filestream = new FileStream("log.txt", FileMode.Append);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            foreach (var file in files)
            {
                var p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.FileName = "ffprobe.exe";
                p.StartInfo.Arguments = $"-v error -select_streams v:0 -show_entries format=size -of default=noprint_wrappers=1:nokey=1 \"{file}\"";
                p.Start();
                double size = Convert.ToInt64(p.StandardOutput.ReadToEnd());
                p.WaitForExit();
                /*p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.FileName = "ffprobe.exe";
                p.StartInfo.Arguments = $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file}\"";
                p.Start();
                double duration = Convert.ToDouble(p.StandardOutput.ReadToEnd());
                p.WaitForExit();*
                int limit = Convert.ToInt32(args[1]);
                int segCount = (int)Math.Ceiling(duration / ((duration / size) * limit));
                limit = (int)Math.Ceiling(duration / segCount);*/
                int limit = Convert.ToInt32(args[1]);
                int segCount = (int)Math.Ceiling(size/limit);
                limit = (int)Math.Ceiling(size/segCount);


                var name = Path.GetFileNameWithoutExtension(file);
                double dur = 0;

                for (int i = 0; i < segCount; i++)
                {
                    p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.FileName = "ffmpeg.exe";
                    //p.StartInfo.Arguments = $"-y -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s mov_text -segment_time {limit} -f segment -reset_timestamps 1 \"{args[2]}\\{name} PART %1d.mp4\" -loglevel error";
                    string ss = "";
                    if(i>0)
                    {
                        var probe = new Process();
                        probe.StartInfo.UseShellExecute = false;
                        probe.StartInfo.RedirectStandardOutput = true;
                        probe.StartInfo.FileName = "ffprobe.exe";
                        probe.StartInfo.Arguments = $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{args[2]}\\{name} PART{i-1}.mp4\"";
                        probe.Start();
                        dur += Convert.ToDouble(probe.StandardOutput.ReadToEnd())-4;
                        ss = " -ss "+dur;
                        probe.WaitForExit();
                    }
                    p.StartInfo.Arguments = $"-y{ss} -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s mov_text -fs {limit} -reset_timestamps 1 \"{args[2]}\\{name} PART{i}.mp4\" -loglevel error";
                    p.Start();
                    var result = p.StandardOutput.ReadToEnd();
                    var error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (error!="")
                    {
                        p = new Process();
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.RedirectStandardError = true;
                        p.StartInfo.FileName = "ffmpeg.exe";
                        p.StartInfo.Arguments = $"-y{ss} -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s dvdsub -fs {limit} -reset_timestamps 1 \"{args[2]}\\{name} PART{i}.mp4\" -loglevel error";
                        p.Start();
                        result = p.StandardOutput.ReadToEnd();
                        error = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                    }
                    if (error!="")
                    {
                        p = new Process();
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.RedirectStandardError = true;
                        p.StartInfo.FileName = "ffmpeg.exe";
                        p.StartInfo.Arguments = $"-y{ss} -i \"{file}\" -c copy -fs {limit} -reset_timestamps 1 \"{args[2]}\\{name} PART{i}.mp4\" -loglevel error";
                        p.Start();
                        result = "Subtitle Skipped "+p.StandardOutput.ReadToEnd();
                        error = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                    }
                    if (result == "") result = "Processed";
                    streamwriter.WriteLine($"{name};{i};{result};{error}");
                    Console.WriteLine($"{name} _ {i} : {result} {error}");
                    Console.WriteLine("END");
                }
                p.Dispose();
            }
            streamwriter.Close();
            filestream.Close();
            Console.ReadLine();
        }
    }
}
