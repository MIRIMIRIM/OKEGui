﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OKEGui.Utils;
using OKEGui.JobProcessor;
using System.Collections.Generic;

namespace OKEGui
{
    public class X265Encoder : CommandlineVideoEncoder
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger("X265Encoder");
        private readonly string X265Path = "";
        private readonly string vspipePath = "";

        public X265Encoder(VideoJob job) : base()
        {
            this.job = job;
            this.NumberOfFrames = job.NumberOfFrames;

            executable = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            if (File.Exists(job.EncoderPath))
            {
                this.X265Path = job.EncoderPath;
            }

            // 获取VSPipe路径
            this.vspipePath = Initializer.Config.vspipePath;

            commandLine = BuildCommandline(job.EncodeParam, job.NumaNode, job.VspipeArgs);
        }

        public override void ProcessLine(string line, StreamType stream)
        {
            if (line.Contains("x265 [error]:"))
            {
                Logger.Error(line);
                OKETaskException ex = new OKETaskException(Constants.x265ErrorSmr);
                ex.progress = 0.0;
                ex.Data["X265_ERROR"] = line.Substring(14);
                throw ex;
            }

            if (line.Contains("Error: fwrite() call failed when writing frame: "))
            {
                Logger.Error(line);
                OKETaskException ex = new OKETaskException(Constants.x265CrashSmr);
                throw ex;
            }

            if (line.ToLowerInvariant().Contains("encoded"))
            {
                Logger.Debug(line);
                Regex rf = new Regex("encoded ([0-9]+) frames in ([0-9]+.[0-9]+)s \\(([0-9]+.[0-9]+) fps\\), ([0-9]+.[0-9]+) kb/s, Avg QP:(([0-9]+.[0-9]+))");

                var result = rf.Split(line);

                long reportedFrames = long.Parse(result[1]);

                // 这里是平均速度
                if (!base.setSpeed(result[3]))
                {
                    return;
                }

                Debugger.Log(0, "EncodeFinish", result[3] + "fps\n");

                base.encodeFinish(reportedFrames);
            }

            Regex regOfficial = new Regex("([0-9]+) frames: ([0-9]+.[0-9]+) fps, ([0-9]+.[0-9]+) kb/s", RegexOptions.IgnoreCase);
            Regex regAsuna = new Regex("([0-9]+)/[0-9]+ frames, ([0-9]+.[0-9]+) fps, ([0-9]+.[0-9]+) kb/s", RegexOptions.IgnoreCase);

            string[] status;

            if (regOfficial.Split(line).Length >= 3)
            {
                status = regOfficial.Split(line);
            }
            else if (regAsuna.Split(line).Length >= 3)
            {
                status = regAsuna.Split(line);
            }
            else
            {
                Logger.Debug(line);
                return;
            }

            if (!base.setFrameNumber(status[1], true))
            {
                return;
            }

            base.setBitrate(status[3], "kb/s");

            if (!base.setSpeed(status[2]))
            {
                return;
            }
        }

        private string BuildCommandline(string extractParam, int numaNode, List<string> vspipeArgs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("/c \"start \"foo\" /b /wait ");
            if (!Initializer.Config.singleNuma)
            {
                sb.Append("/affinity 0xFFFFFFFFFFFFFFFF /node ");
                sb.Append(numaNode.ToString());
            }
            // 构建vspipe参数
            sb.Append(" \"" + vspipePath + "\"");
            sb.Append(" --y4m");
            foreach (string arg in vspipeArgs)
            {
                sb.Append($" --arg \"{arg}\"");
            }
            sb.Append(" \"" + job.Input + "\"");
            sb.Append(" - |");

            // 构建x265参数
            sb.Append(" \"" + X265Path + "\"");
            if (Initializer.Config.avx512 && !extractParam.ToLower().Contains("--asm"))
            {
                sb.Append(" --asm avx512");
            }
            sb.Append(" --y4m " + extractParam + " -o");
            sb.Append(" \"" + job.Output + "\" -");
            sb.Append("\"");

            return sb.ToString();
        }

    }
}
