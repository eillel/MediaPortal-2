#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MediaPortal.Common;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.PathManager;
using MediaPortal.Common.Threading;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib.Data;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib.Data.Banner;
using MediaPortal.Extensions.OnlineLibraries.TheTvDB;

namespace MediaPortal.Extensions.OnlineLibraries
{
  /// <summary>
  /// <see cref="SeriesTvDbMatcher"/> is used to look up online series information from TheTvDB.com.
  /// </summary>
  public class SeriesTvDbMatcher : SeriesMatcher<TvdbBanner, TvdbLanguage>
  {
    #region Static instance

    public static SeriesTvDbMatcher Instance
    {
      get { return ServiceRegistration.Get<SeriesTvDbMatcher>(); }
    }

    #endregion

    #region Constants

    public static string CACHE_PATH = ServiceRegistration.Get<IPathManager>().GetPath(@"<DATA>\TvDB\");
    protected static TimeSpan MAX_MEMCACHE_DURATION = TimeSpan.FromMinutes(1);
    protected static TimeSpan MIN_REFRESH_INTERVAL = TimeSpan.FromHours(12);

    #endregion

    #region Init

    public SeriesTvDbMatcher() : 
      base(CACHE_PATH, MAX_MEMCACHE_DURATION)
    {
    }

    public override bool InitWrapper()
    {
      try
      {
        TvDbWrapper wrapper = new TvDbWrapper();
        // Try to lookup online content in the configured language
        CultureInfo currentCulture = ServiceRegistration.Get<ILocalization>().CurrentCulture;
        wrapper.SetPreferredLanguage(currentCulture.TwoLetterISOLanguageName);
        if (wrapper.Init(CACHE_PATH))
        {
          _wrapper = wrapper;
          return true;
        }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SeriesTvDbMatcher: Error initializing wrapper", ex);
      }
      return false;
    }

    #endregion

    #region Translators

    protected override bool SetSeriesId(SeriesInfo series, string id)
    {
      if (!string.IsNullOrEmpty(id))
      {
        series.TvdbId = Convert.ToInt32(id);
        return true;
      }
      return false;
    }

    protected override bool SetSeriesId(EpisodeInfo episode, string id)
    {
      if (!string.IsNullOrEmpty(id))
      {
        episode.SeriesTvdbId = Convert.ToInt32(id);
        return true;
      }
      return false;
    }

    protected override bool GetSeriesId(SeriesInfo series, out string id)
    {
      id = null;
      if (series.TvdbId > 0)
        id = series.TvdbId.ToString();
      return id != null;
    }

    protected override bool GetSeriesEpisodeId(EpisodeInfo episode, out string id)
    {
      id = null;
      if (episode.TvdbId > 0)
        id = episode.TvdbId.ToString();
      return id != null;
    }

    protected override bool GetCompanyId(CompanyInfo company, out string id)
    {
      id = null;
      if (company.TvdbId > 0)
        id = company.TvdbId.ToString();
      return id != null;
    }

    protected override bool GetPersonId(PersonInfo person, out string id)
    {
      id = null;
      if (person.TvdbId > 0)
        id = person.TvdbId.ToString();
      return id != null;
    }

    #endregion

    #region Fields

    protected bool _useUniversalLanguage = false; // Universal language often leads to unwanted cover languages (i.e. russian)

    #endregion

    #region Caching

    protected override void RefreshCache()
    {
      IThreadPool threadPool = ServiceRegistration.Get<IThreadPool>(false);
      if (threadPool != null)
      {
        ServiceRegistration.Get<ILogger>().Debug("SeriesTvDbMatcher: Refreshing local cache");
        threadPool.Add(() =>
        {
          if (Init())
            ((TvDbWrapper)_wrapper).UpdateCache();
        });
      }
    }

    #endregion

    #region FanArt

    public override List<string> GetFanArtFiles<T>(T infoObject, string scope, string type)
    {
      List<string> fanartFiles = new List<string>();
      if (scope == FanArtScope.Series)
      {
        SeriesInfo series = infoObject as SeriesInfo;
        if (series != null && series.TvdbId > 0)
        {
          string path = Path.Combine(CACHE_PATH, series.TvdbId.ToString());
          if (Directory.Exists(path))
          {
            if (type == FanArtType.Banners)
            {
              fanartFiles.AddRange(Directory.GetFiles(path, "img_graphical_ *.jpg"));
              fanartFiles.AddRange(Directory.GetFiles(path, "img_text_*.jpg"));
            }
            else if (type == FanArtType.Posters)
            {
              fanartFiles.AddRange(Directory.GetFiles(path, "img_posters_*.jpg"));
            }
            else if (type == FanArtType.Backdrops)
            {
              fanartFiles.AddRange(Directory.GetFiles(path, "img_fan-*.jpg"));
            }
          }
        }
      }
      else if (scope == FanArtScope.Season)
      {
        SeasonInfo season = infoObject as SeasonInfo;
        if (season != null && season.SeriesTvdbId > 0 && season.SeasonNumber.HasValue)
        {
          string path = Path.Combine(CACHE_PATH, season.SeriesTvdbId.ToString());
          if (Directory.Exists(path))
          {
            if (type == FanArtType.Banners)
            {
              fanartFiles.AddRange(Directory.GetFiles(path, string.Format("img_seasonswide_{0}-{1}*.jpg", season.SeriesTvdbId, season.SeasonNumber.Value)));
            }
            else if (type == FanArtType.Posters)
            {
              fanartFiles.AddRange(Directory.GetFiles(path, string.Format("img_seasons_{0}-{1}*.jpg", season.SeriesTvdbId, season.SeasonNumber.Value)));
            }
          }
        }
      }
      else if (scope == FanArtScope.Episode)
      {
        EpisodeInfo episode = infoObject as EpisodeInfo;
        if (episode != null && episode.TvdbId > 0 && episode.SeriesTvdbId > 0)
        {
          string path = Path.Combine(CACHE_PATH, episode.SeriesTvdbId.ToString());
          if (Directory.Exists(path))
          {
            if (type == FanArtType.Thumbnails)
            {
              string file = Path.Combine(path, string.Format("img_episodes_{0}-{1}.jpg", episode.SeriesTvdbId, episode.TvdbId));
              if (File.Exists(file))
                fanartFiles.Add(file);
            }
          }
        }
      }
      else if (scope == FanArtScope.Actor)
      {
        PersonInfo person = infoObject as PersonInfo;
        if (person != null && person.TvdbId > 0)
        {
          string path = Path.Combine(CACHE_PATH, person.TvdbId.ToString());
          if (Directory.Exists(path))
          {
            if (type == FanArtType.Thumbnails)
            {
              if (Directory.Exists(path))
                fanartFiles.AddRange(Directory.GetFiles(path, string.Format(@"img_actors_{0}.jpg", person.TvdbId), SearchOption.AllDirectories));
            }
          }
        }
      }
      return fanartFiles;
    }

    protected override int SaveFanArtImages(string id, IEnumerable<TvdbBanner> images, string scope, string type)
    {
      if (images == null)
        return 0;

      return SaveBanners(images, _wrapper.PreferredLanguage);
    }

    protected override int SaveSeriesSeasonFanArtImages(string id, int seasonNo, IEnumerable<TvdbBanner> images, string scope, string type)
    {
      if (images == null)
        return 0;

      return SaveBanners(images, _wrapper.PreferredLanguage);
    }

    protected override int SaveSeriesEpisodeFanArtImages(string id, int seasonNo, int episodeNo, IEnumerable<TvdbBanner> images, string scope, string type)
    {
      if (images == null)
        return 0;

      return SaveBanners(images, _wrapper.PreferredLanguage);
    }

    private int SaveBanners(IEnumerable<TvdbBanner> banners, TvdbLanguage language)
    {
      int idx = 0;
      foreach (TvdbBanner tvdbBanner in banners)
      {
        if (tvdbBanner.Language != language && language != null && tvdbBanner.Language != null)
          continue;

        if (idx++ >= MAX_FANART_IMAGES)
          break;

        if (!tvdbBanner.IsLoaded)
        {
          // We need the image only loaded once, later we will access the cache directly
          try
          {
            tvdbBanner.LoadBanner();
            tvdbBanner.UnloadBanner();
          }
          catch (Exception ex)
          {
            ServiceRegistration.Get<ILogger>().Warn("SeriesTvDbMatcher: Exception saving FanArt image", ex);
          }
        }
      }
      if (idx > 0)
      {
        ServiceRegistration.Get<ILogger>().Debug( @"SeriesTvDbMatcher Download: Saved {0} banners", idx);
        return idx;
      }

      // Try fallback languages if no images found for preferred
      if (language != TvdbLanguage.UniversalLanguage && language != TvdbLanguage.DefaultLanguage)
      {
        if (_useUniversalLanguage)
        {
          idx = SaveBanners(banners, TvdbLanguage.UniversalLanguage);
          if (idx > 0)
            return idx;
        }

        idx = SaveBanners(banners, TvdbLanguage.DefaultLanguage);
      }
      ServiceRegistration.Get<ILogger>().Debug(@"SeriesTvDbMatcher Download: Saved {0} banners", idx);
      return idx;
    }

    #endregion
  }
}
