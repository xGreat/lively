﻿using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lively.Common.Helpers
{
    public static class GithubUtil
    {
        /// <summary>
        /// Get latest release from github after given delay.
        /// </summary>
        /// <param name="repositoryName"></param>
        /// <param name="userName"></param>
        /// <param name="startDelay"></param>
        /// <returns></returns>
        public static async Task<Release> GetLatestRelease(string repositoryName, string userName, int startDelay = 45000)
        {
            //wait for computer startup.. so that user and network is ready.
            await Task.Delay(startDelay);
            GitHubClient client = new GitHubClient(new ProductHeaderValue(repositoryName));
            var releases = await client.Repository.Release.GetAll(userName, repositoryName);
            var latest = releases[0];

            return latest;
        }

        public static async Task<IEnumerable<(string Name, string Url)>> GetAssetUrl(Release release, string repositoryName, string userName)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(repositoryName));
            var allAssets = await client.Repository.Release.GetAllAssets(userName, repositoryName, release.Id);
            return allAssets.Select(x => (x.Name, Url: x.BrowserDownloadUrl));
        }

        public static async Task<IEnumerable<(string Name, string Url)>> GetLatestAsset(string repositoryName, string userName, int startDelay = 1000)
        {
            Release release = await GetLatestRelease(repositoryName, userName, startDelay);
            return await GetAssetUrl(release, repositoryName, userName);
        }

        public static Version GetVersion(Release release)
        {
            return new Version(Regex.Replace(release.TagName, "[A-Za-z ]", ""));
        }

        /// <summary>
        /// Compare current software assembly version with given release version.
        /// </summary>
        /// <param name="release"></param>
        /// <returns> =0 same, >0 git greater, <0 pgm greater</returns>
        public static int CompareAssemblyVersion(Release release)
        {
            string tmp = Regex.Replace(release.TagName, "[A-Za-z ]", "");
            var gitVersion = new Version(tmp);
            var appVersion = new Version(System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            var result = gitVersion.CompareTo(appVersion);

            return result;
        }

        public static int CompareAssemblyVersion(Version version)
        {
            var appVersion = new Version(System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            return version.CompareTo(appVersion);
        }

        /// <summary>
        /// String Contains method with StringComparison property.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="substring"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        private static bool Contains(String str, String substring,
                                    StringComparison comp)
        {
            if (substring == null)
                throw new ArgumentNullException("substring",
                                             "substring cannot be null.");
            else if (!Enum.IsDefined(typeof(StringComparison), comp))
                throw new ArgumentException("comp is not a member of StringComparison",
                                         "comp");

            return str.IndexOf(substring, comp) >= 0;
        }
    }
}
