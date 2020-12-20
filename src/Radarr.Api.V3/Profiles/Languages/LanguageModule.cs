using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Languages;
using Radarr.Http;

namespace Radarr.Api.V3.Profiles.Languages
{
    public class LanguageModule : RadarrRestModule<LanguageResource>
    {
        public LanguageModule()
        {
            GetResourceAll = GetAll;
            GetResourceById = GetById;
        }

        private LanguageResource GetById(int id)
        {
            var language = (Language)id;

            return new LanguageResource
            {
                Id = (int)language,
                Name = language.ToString()
            };
        }

        private List<LanguageResource> GetAll()
        {
            var languageResources = Language.All.Select(l => new LanguageResource
            {
                Id = (int)l,
                Name = l.ToString()
            })
                                    .OrderBy(l => l.Name)
                                    .ToList();

            MoveCustomToTop(languageResources);

            return languageResources;
        }

        private void MoveCustomToTop(List<LanguageResource> languageResources)
        {
            var customLanguages = languageResources.FindAll(l => l.Id < 0).OrderBy(l => l.Id);

            foreach (var l in customLanguages)
            {
                languageResources.Remove(l);
                languageResources.Insert(0, l);
            }
        }
    }
}
