﻿using System;
using System.Collections.Generic;
using System.Linq;
using HttpServer;
using HttpServer.Exceptions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Plugins.MP2Extended.Utils;
using MediaPortal.Plugins.MP2Extended.WSS.StreamInfo;
using MediaPortal.Plugins.Transcoding.Aspects;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.WSS.json.StreamInfo
{
  internal class GetMediaInfo : IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request)
    {
      HttpParam httpParam = request.Param;
      string id = httpParam["itemId"].Value;
      if (id == null)
        throw new BadRequestException("GetMediaInfo: no itemId is null");

      ISet<Guid> necessaryMIATypes = new HashSet<Guid>();
      necessaryMIATypes.Add(MediaAspect.ASPECT_ID);
      necessaryMIATypes.Add(ProviderResourceAspect.ASPECT_ID);
      necessaryMIATypes.Add(ImporterAspect.ASPECT_ID);

      ISet<Guid> optionalMIATypes = new HashSet<Guid>();
      optionalMIATypes.Add(VideoAspect.ASPECT_ID);
      optionalMIATypes.Add(AudioAspect.ASPECT_ID);
      optionalMIATypes.Add(ImageAspect.ASPECT_ID);
      optionalMIATypes.Add(TranscodeItemAudioAspect.ASPECT_ID);
      optionalMIATypes.Add(TranscodeItemImageAspect.ASPECT_ID);
      optionalMIATypes.Add(TranscodeItemVideoAspect.ASPECT_ID);

      MediaItem item = GetMediaItems.GetMediaItemById(id, necessaryMIATypes, optionalMIATypes);

      if (item == null)
        throw new BadRequestException(String.Format("GetMediaInfo: No MediaItem found with id: {0}", id));

      long duration = 0;
      string container = string.Empty;
      List<WebVideoStream> webVideoStreams = new List<WebVideoStream>();
      List<WebAudioStream> webAudioStreams = new List<WebAudioStream>();
      List<WebSubtitleStream> webSubtitleStreams = new List<WebSubtitleStream>();

      // decide which type of media item we have
      if (item.Aspects.ContainsKey(VideoAspect.ASPECT_ID))
      {
        var videoAspect = item.Aspects[VideoAspect.ASPECT_ID];
        duration = (long)videoAspect[VideoAspect.ATTR_DURATION];

        // Video Stream
        WebVideoStream webVideoStream = new WebVideoStream();
        webVideoStream.Codec = (string)videoAspect[VideoAspect.ATTR_VIDEOENCODING];
        webVideoStream.DisplayAspectRatio = Convert.ToDecimal((float)videoAspect[VideoAspect.ATTR_ASPECTRATIO]);
        webVideoStream.DisplayAspectRatioString = AspectRatioHelper.AspectRatioToString((float)videoAspect[VideoAspect.ATTR_ASPECTRATIO]);
        webVideoStream.Height = (int)videoAspect[VideoAspect.ATTR_HEIGHT];
        webVideoStream.Width = (int)videoAspect[VideoAspect.ATTR_WIDTH];
        webVideoStreams.Add(webVideoStream);

        if (item.Aspects.ContainsKey(TranscodeItemVideoAspect.ASPECT_ID))
        {
          var transcodeVideoAspect = item.Aspects[TranscodeItemVideoAspect.ASPECT_ID];
          webVideoStream.ID = 0;
          webVideoStream.Index = 0;
          //webVideoStream.Interlaced = transcodeVideoAspect[TranscodeItemVideoAspect.];

          container = (string)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_CONTAINER];

          // Audio streams
          var audioStreams = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_AUDIOSTREAMS];
          var audioChannels = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_AUDIOCHANNELS];
          var audioCodecs = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_AUDIOCODECS];
          var audioLanguages = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_AUDIOLANGUAGES];
          if (audioStreams != null)
            for (int i = 0; i < audioStreams.Count; i++)
            {
              WebAudioStream webAudioStream = new WebAudioStream();
              if (audioChannels != null)
                webAudioStream.Channels = int.Parse(audioChannels.Cast<string>().ToList().Count < i ? audioChannels.Cast<string>().ToList()[i] : audioChannels.Cast<string>().ToList()[0]);
              if (audioCodecs != null)
                webAudioStream.Codec = audioCodecs.Cast<string>().ToList().Count < i ? audioCodecs.Cast<string>().ToList()[i] : audioCodecs.Cast<string>().ToList()[0];
              webAudioStream.ID = i;
              webAudioStream.Index = int.Parse(audioStreams.Cast<string>().ToList()[i]);
              if (audioLanguages != null)
              {
                string language = audioLanguages.Cast<string>().ToList()[i] == string.Empty ? "undef" : audioLanguages.Cast<string>().ToList()[i];
                webAudioStream.Language = language;
                webAudioStream.LanguageFull = language;
                webAudioStream.Title = language;
              }

              webAudioStreams.Add(webAudioStream);
            }

          // Subtitles (embedded)
          var subtitleStreams = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_EMBEDDED_SUBSTREAMS];
          var subtitleLanguages = (HashSet<object>)transcodeVideoAspect[TranscodeItemVideoAspect.ATTR_EMBEDDED_SUBLANGUAGES];
          if (subtitleStreams != null)
            for (int i = 0; i < subtitleStreams.Count; i++)
            {
              WebSubtitleStream webSubtitleStream = new WebSubtitleStream();
              webSubtitleStream.Filename = "embedded";
              webSubtitleStream.ID = i;
              webSubtitleStream.Index = i;
              if (subtitleLanguages != null)
              {
                string language = subtitleLanguages.Cast<string>().ToList()[i] == string.Empty ? "undef" : subtitleLanguages.Cast<string>().ToList()[i];
                webSubtitleStream.Language = language;
                webSubtitleStream.LanguageFull = language;
              }


              webSubtitleStreams.Add(webSubtitleStream);
            }
        }

      }

      // Audio File
      if (item.Aspects.ContainsKey(AudioAspect.ASPECT_ID))
      {
        var audioAspect = item.Aspects[AudioAspect.ASPECT_ID];
        duration = (long)audioAspect[AudioAspect.ATTR_DURATION];
        if (item.Aspects.ContainsKey(TranscodeItemAudioAspect.ASPECT_ID))
        {
          container = (string)item.Aspects[TranscodeItemAudioAspect.ASPECT_ID][TranscodeItemAudioAspect.ATTR_CONTAINER];
        }
      }

      // Image File
      if (item.Aspects.ContainsKey(ImageAspect.ASPECT_ID))
      {
        var imageAspect = item.Aspects[ImageAspect.ASPECT_ID];
        if (item.Aspects.ContainsKey(TranscodeItemImageAspect.ASPECT_ID))
        {
          container = (string)item.Aspects[TranscodeItemImageAspect.ASPECT_ID][TranscodeItemImageAspect.ATTR_CONTAINER];
        }
      }

      WebMediaInfo webMediaInfo = new WebMediaInfo
      {
        Duration = duration,
        Container = container,
        VideoStreams = webVideoStreams,
        AudioStreams = webAudioStreams,
        SubtitleStreams = webSubtitleStreams
      };


      return webMediaInfo;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}