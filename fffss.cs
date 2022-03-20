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
    internal class fffss
    {
        // args{ dir of files to split, max size in bytes, dir of output }
        static void Main(string[] args)
        {
            var files = Directory.GetFiles(args[0]);
            FileStream filestream = new FileStream("log.csv", FileMode.Append);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            foreach (var file in files)
            {
                var p = NewProcess("ffprobe.exe", $"-v error -select_streams v:0 -show_entries format=size -of default=noprint_wrappers=1:nokey=1 \"{file}\"");
                p.Start();
                double size = Convert.ToInt64(p.StandardOutput.ReadToEnd());
                p.WaitForExit();
                p = NewProcess("ffprobe.exe", $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file}\"");
                p.Start();
                double duration = Convert.ToDouble(p.StandardOutput.ReadToEnd());
                p.WaitForExit();
                int limit = Convert.ToInt32(args[1]);
                int segCount = (int)Math.Ceiling(size/limit);
                limit = (int)Math.Ceiling(size/segCount);

                var name = Path.GetFileNameWithoutExtension(file);
                double dur = 0;

                bool useAlt = false;
                for (int i = 0; i < segCount; i++)
                {
                    useAlt = false;
                    string ss = "";
                    if(i>0)
                    {
                        var probe = NewProcess("ffprobe.exe", $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{args[2]}\\{name} PART{i - 1}.mp4\"");
                        probe.Start();
                        dur += Convert.ToDouble(probe.StandardOutput.ReadToEnd())-4;
                        ss = " -ss "+dur;
                        probe.WaitForExit();
                        probe.Dispose();
                    }
                    string result="", error="";
                    Run(useAlt, file, name, limit, args[2], ref result, ref error, ss, i);
                    if(i==1)
                    {
                        var probe = NewProcess("ffprobe.exe", $"-v error -select_streams v:0 -show_entries format=size -of default=noprint_wrappers=1:nokey=1 \"{args[2]}\\{name} PART0.mp4\"");
                        probe.Start();
                        double d0 = Convert.ToDouble(probe.StandardOutput.ReadToEnd());
                        probe = NewProcess("ffprobe.exe", $"-v error -select_streams v:0 -show_entries format=size -of default=noprint_wrappers=1:nokey=1 \"{args[2]}\\{name} PART1.mp4\"");
                        probe.Start();
                        double d1 = Convert.ToDouble(probe.StandardOutput.ReadToEnd());
                        probe.WaitForExit();
                        probe.Dispose();
                        if (Math.Abs(d1-d0)>size*0.1)
                        {
                            useAlt = true;
                            break;
                        }
                    }
                    if (result == "") result = "Processed";
                    streamwriter.WriteLine($"{DateTime.Now},\"{name} - {i}\",{result},{error}");
                    Console.WriteLine($"{name} PART {i} {result} {error}");
                }
                if(useAlt)
                {
                    limit = Convert.ToInt32(args[1]);
                    segCount = (int)Math.Ceiling(duration / ((duration / size) * limit));
                    limit = (int)Math.Ceiling(duration / segCount);
                    string result = "", error = "";
                    Run(useAlt, file, name, limit, args[2], ref result, ref error);
                    streamwriter.WriteLine($"{DateTime.Now},\"{name}\",{result},{error}");
                    Console.WriteLine($"{name} {result} {error}");
                }
                p.Dispose();
            }
            streamwriter.Close();
            filestream.Close();
            Console.WriteLine("END");
        }
        static Process NewProcess(string fileName, string arg)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = arg;
            return p;
        }
        static void Run(bool alt, string file, string name, int limit, string outDir, ref string result, ref string error, string ss = "", int i=0)
        {
            var p = NewProcess("ffmpeg.exe", alt?
                $"-y -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s mov_text -segment_time {limit} -f segment -reset_timestamps 1 \"{outDir}\\{name} PART%1d.mp4\" -loglevel error" : 
                $"-y{ss} -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s mov_text -fs {limit} -reset_timestamps 1 \"{outDir}\\{name} PART{i}.mp4\" -loglevel error");
            p.Start();
            result = (alt? "Warning Alt method used - please double check file size" :"") + p.StandardOutput.ReadToEnd();
            error = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (error != "")
            {
                p = NewProcess("ffmpeg.exe", alt?
                    $"-y -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s dvdsub -segment_time {limit} -f segment -reset_timestamps 1 \"{outDir}\\{name} PART%1d.mp4\" -loglevel error":
                    $"-y{ss} -i \"{file}\" -map 0 -c:v copy -c:a copy -c:s dvdsub -fs {limit} -reset_timestamps 1 \"{outDir}\\{name} PART{i}.mp4\" -loglevel error");
                p.Start();
                result = (alt ? "Warning Alt method used - please double check file size" : "") + p.StandardOutput.ReadToEnd();
                error = p.StandardError.ReadToEnd();
                p.WaitForExit();
            }
            if (error != "")
            {
                p = NewProcess("ffmpeg.exe", alt?
                    $"-y -i \"{file}\" -c copy -segment_time {limit} -f segment -reset_timestamps 1 \"{outDir}\\{name} PART%1d.mp4\" -loglevel error":
                    $"-y{ss} -i \"{file}\" -c copy -fs {limit} -reset_timestamps 1 \"{outDir}\\{name} PART{i}.mp4\" -loglevel error");
                p.Start();
                result = (alt ? "Warning Alt method used - please double check file size _ " : "") + "Subtitle Skipped " + p.StandardOutput.ReadToEnd();
                error = p.StandardError.ReadToEnd();
                p.WaitForExit();
            }
            p.Dispose();
            return;
        }
    }
}
