﻿namespace FFmpeg
{
    using FFmpeg.AutoGen;
    using System;
    using System.Runtime.CompilerServices;
    using Unosquare.FFplaySharp.Primitives;

    public unsafe sealed class FormatContext : UnmanagedCountedReference<AVFormatContext>
    {
        public FormatContext([CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = default)
            : base(filePath, lineNumber)
        {
            Update(ffmpeg.avformat_alloc_context());
            Streams = new(this);
        }

        public AVIOInterruptCB_callback_func InterruptCallback
        {
            get => Pointer->interrupt_callback.callback;
            set => Pointer->interrupt_callback.callback = value;
        }

        public StreamCollection Streams { get; }

        public int StreamCount => Convert.ToInt32(Pointer->nb_streams);

        public int ChapterCount => Convert.ToInt32(Pointer->nb_chapters);

        public long Duration => Pointer->duration;

        public double DurationSeconds => Duration / Clock.TimeBaseMicros;

        public void InjectGlobalSideData() => ffmpeg.av_format_inject_global_side_data(Pointer);

        public AVProgram* FindProgramFromStream(int streamIndex) =>
            ffmpeg.av_find_program_from_stream(Pointer, null, streamIndex);

        public AVRational GuessFrameRate(Stream stream) => ffmpeg.av_guess_frame_rate(Pointer, stream.Pointer, null);

        public AVRational GuessAspectRatio(Stream stream, AVFrame* frame) =>
            ffmpeg.av_guess_sample_aspect_ratio(Pointer, stream.Pointer, frame);

        public int OpenInput(string filePath, InputFormat format, FFDictionary formatOptions)
        {
            var context = Pointer;
            var formatOptionsPtr = formatOptions.Pointer;
            var resultCode = ffmpeg.avformat_open_input(&context, filePath, format.Pointer, &formatOptionsPtr);
            Update(context);
            formatOptions.Update(formatOptionsPtr);
            
            return resultCode;
        }

        protected override unsafe void ReleaseInternal(AVFormatContext* pointer) =>
            ffmpeg.avformat_close_input(&pointer);

    }
}
