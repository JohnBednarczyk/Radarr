using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using Ninject;
using NLog;
using NzbDrone.Core.Model;
using NzbDrone.Core.Providers.Core;

namespace NzbDrone.Core.Providers.Indexer
{
    public abstract class IndexerBase
    {
        protected readonly Logger _logger;
        private readonly HttpProvider _httpProvider;
        protected readonly ConfigProvider _configProvider;

        private static readonly Regex TitleSearchRegex = new Regex(@"[\W]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Inject]
        protected IndexerBase(HttpProvider httpProvider, ConfigProvider configProvider)
        {
            _httpProvider = httpProvider;
            _configProvider = configProvider;

            _logger = LogManager.GetLogger(GetType().ToString());
        }

        public IndexerBase()
        {

        }

        /// <summary>
        ///   Gets the name for the feed
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        ///   Gets the source URL for the feed
        /// </summary>
        protected abstract string[] Urls { get; }


        /// <summary>
        /// Gets the credential.
        /// </summary>
        protected virtual NetworkCredential Credentials
        {
            get { return null; }
        }


        protected abstract IList<String> GetEpisodeSearchUrls(string seriesTitle, int seasonNumber, int episodeNumber);
        protected abstract IList<String> GetDailyEpisodeSearchUrls(string seriesTitle, DateTime date);
        protected abstract IList<String> GetSeasonSearchUrls(string seriesTitle, int seasonNumber);
        protected abstract IList<String> GetPartialSeasonSearchUrls(string seriesTitle, int seasonNumber, int episodeWildcard);

        /// <summary>
        /// This method can be overwritten to provide indexer specific info parsing
        /// </summary>
        /// <param name="item">RSS item that needs to be parsed</param>
        /// <param name="currentResult">Result of the built in parse function.</param>
        /// <returns></returns>
        protected virtual EpisodeParseResult CustomParser(SyndicationItem item, EpisodeParseResult currentResult)
        {
            return currentResult;
        }

        /// <summary>
        ///   Generates direct link to download an NZB
        /// </summary>
        /// <param name = "item">RSS Feed item to generate the link for</param>
        /// <returns>Download link URL</returns>
        protected abstract string NzbDownloadUrl(SyndicationItem item);

        /// <summary>
        ///   Fetches RSS feed and process each news item.
        /// </summary>
        public virtual IList<EpisodeParseResult> FetchRss()
        {
            _logger.Debug("Fetching feeds from " + Name);

            var result = new List<EpisodeParseResult>();


            result = Fetch(Urls);


            _logger.Info("Finished processing feeds from " + Name);
            return result;
        }

        public virtual IList<EpisodeParseResult> FetchSeason(string seriesTitle, int seasonNumber)
        {
            _logger.Debug("Searching {0} for {1}-Season {2}", Name, seriesTitle, seasonNumber);

            var searchUrls = GetSeasonSearchUrls(GetQueryTitle(seriesTitle), seasonNumber);
            var result = Fetch(searchUrls);

            _logger.Info("Finished searching {0} for {1}-S{2}, Found {3}", Name, seriesTitle, seasonNumber, result.Count);
            return result;
        }

        public virtual IList<EpisodeParseResult> FetchPartialSeason(string seriesTitle, int seasonNumber, int episodePrefix)
        {
            _logger.Debug("Searching {0} for {1}-Season {2}, Prefix: {3}", Name, seriesTitle, seasonNumber, episodePrefix);


            var searchUrls = GetPartialSeasonSearchUrls(GetQueryTitle(seriesTitle), seasonNumber, episodePrefix);

            var result = Fetch(searchUrls);

            _logger.Info("Finished searching {0} for {1}-S{2}, Found {3}", Name, seriesTitle, seasonNumber, result.Count);
            return result;
        }

        public virtual IList<EpisodeParseResult> FetchEpisode(string seriesTitle, int seasonNumber, int episodeNumber)
        {
            _logger.Debug("Searching {0} for {1}-S{2:00}E{3:00}", Name, seriesTitle, seasonNumber, episodeNumber);

            var searchUrls = GetEpisodeSearchUrls(GetQueryTitle(seriesTitle), seasonNumber, episodeNumber);

            var result = Fetch(searchUrls);

            _logger.Info("Finished searching {0} for {1}-S{2}E{3:00}, Found {4}", Name, seriesTitle, seasonNumber, episodeNumber, result.Count);
            return result;

        }

        public virtual IList<EpisodeParseResult> FetchDailyEpisode(string seriesTitle, DateTime airDate)
        {
            _logger.Debug("Searching {0} for {1}-{2}", Name, seriesTitle, airDate.ToShortDateString());

            var searchUrls = GetDailyEpisodeSearchUrls(GetQueryTitle(seriesTitle), airDate);

            var result = Fetch(searchUrls);

            _logger.Info("Finished searching {0} for {1}-{2}, Found {3}", Name, seriesTitle, airDate.ToShortDateString(), result.Count);
            return result;

        }

        private List<EpisodeParseResult> Fetch(IEnumerable<string> urls)
        {
            var result = new List<EpisodeParseResult>();

            foreach (var url in urls)
            {
                try
                {
                    _logger.Trace("Downloading RSS " + url);

                    var reader = new SyndicationFeedXmlReader(_httpProvider.DownloadStream(url, Credentials));
                    var feed = SyndicationFeed.Load(reader).Items;

                    foreach (var item in feed)
                    {
                        try
                        {
                            var parsedEpisode = ParseFeed(item);
                            if (parsedEpisode != null)
                            {
                                parsedEpisode.NzbUrl = NzbDownloadUrl(item);
                                parsedEpisode.Indexer = Name;
                                parsedEpisode.OriginalString = item.Title.Text;
                                result.Add(parsedEpisode);
                            }
                        }
                        catch (Exception itemEx)
                        {
                            itemEx.Data.Add("FeedUrl", url);
                            itemEx.Data.Add("Item", item.Title);
                            _logger.ErrorException("An error occurred while processing feed item", itemEx);
                        }

                    }
                }
                catch(WebException webException)
                {
                    if (webException.Message.Contains("503"))
                    {
                        _logger.Warn("{0} server is currently unbelievable. {1}", Name, webException.Message);     
                    }
                    else
                    {
                        webException.Data.Add("FeedUrl", url);
                        _logger.ErrorException("An error occurred while processing feed: " + Name, webException);
                    }
                }
                catch (Exception feedEx)
                {
                    feedEx.Data.Add("FeedUrl", url);
                    _logger.ErrorException("An error occurred while processing feed: " + Name, feedEx);
                }
            }

            return result;
        }

        /// <summary>
        ///   Parses the RSS feed item
        /// </summary>
        /// <param name = "item">RSS feed item to parse</param>
        /// <returns>Detailed episode info</returns>
        public EpisodeParseResult ParseFeed(SyndicationItem item)
        {
            var episodeParseResult = Parser.ParseTitle(item.Title.Text);

            return CustomParser(item, episodeParseResult);
        }

        public static string GetQueryTitle(string title)
        {
            var cleanTitle = TitleSearchRegex.Replace(title, "+").Trim('+', ' ');

            //remove any repeating +s
            cleanTitle = Regex.Replace(cleanTitle, @"\+{1,100}", "+");
            return cleanTitle;
        }
    }
}