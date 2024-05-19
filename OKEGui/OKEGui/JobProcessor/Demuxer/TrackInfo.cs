﻿using System;
using System.IO;
using OKEGui.Model;

namespace OKEGui.JobProcessor
{
    public partial class EACDemuxer
    {
        public enum TrackCodec
        {
            Unknown,
            MPEG2,
            H264_AVC,
            H265_HEVC,
            AV1,
            RAW_PCM,
            FLAC,
            AAC,
            DTSMA,
            TRUEHD_AC3,
            AC3,
            DTS,
            EAC3,
            OPUS,
            PGS,
            Chapter,
            VobSub,
            ASS,
            SRT
        }

        public class TrackInfo
        {
            public TrackCodec Codec;
            public int Index;
            public string Information;
            public string RawOutput;
            public string SourceFile;
            public string WorkingPathPrefix;
            public TrackType Type;
            public bool DupOrEmpty;
            public int Length;
            public long FileSize;
            public double MeanVolume;
            public double MaxVolume;

            public string OutFileName
            {
                get
                {
                    var directory = Path.GetDirectoryName(WorkingPathPrefix);
                    var baseName = Path.GetFileNameWithoutExtension(SourceFile);

                    return $"{Path.Combine(directory, baseName)}_{Index}{FileExtension}";
                }
            }

            public string FileExtension
            {
                get
                {
                    TrackCodec type = Codec;
                    return "." + s_eacOutputs.Find(val => val.Codec == type).FileExtension;
                }
            }

            public bool IsEmpty()
            {
                switch (Type)
                {
                    case TrackType.Audio:
                        return MeanVolume < -70 && MaxVolume < -30;
                    case TrackType.Subtitle:
                        return FileSize / Length < 3 * 1024 * 1024 / 3600;
                    default:
                        return FileSize < 64;
                }
            }

            public bool IsDuplicate(in TrackInfo other)
            {
                if (Type != other.Type)
                {
                    return false;
                }
                switch (Type)
                {
                    case TrackType.Audio:
                        return Math.Abs(MeanVolume - other.MeanVolume) < 0.01 && Math.Abs(MaxVolume - other.MaxVolume) < 0.01;
                    default:
                        return FileSize == other.FileSize;
                }
            }

            public void MarkSkipping()
            {
                try
                {
                    File.Move(OutFileName, Path.ChangeExtension(OutFileName, ".bak") + FileExtension);
                }
                catch (Exception)
                {
                    Logger.Warn("无法备份文件，直接删除。如果是重启的任务，这很正常。");
                    File.Delete(OutFileName);
                }
                DupOrEmpty = true;
            }
        }
    }
}
