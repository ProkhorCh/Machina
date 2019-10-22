using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chauffeur;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Services;

namespace Machina.Migrations
{
    [DeliverableName("machina-migrate-multiurl-picker")]
    [DeliverableAlias("machina-mmup")]
    public class MultiUrlPickerMigration : Deliverable
    {
        private readonly IContentService _contentService;

        private readonly IMediaService _mediaService;

        private static readonly string _newContentTypeAlias = "Umbraco.MultiUrlPicker";

        public MultiUrlPickerMigration(
            TextReader reader,
            TextWriter writer,
            IContentService contentService,
            IMediaService mediaService
        ) : base(reader, writer)
        {
            _contentService = contentService;
            _mediaService = mediaService;
        }

        public override Task<DeliverableResponse> Run(string command, string[] args)
        {
            var cliInput = MigrationHelper.ParseCliArgs(args);

            Console.WriteLine("Migrating Umbraco.MultiUrlPicker properties from RJP.MultiUrlPicker format to UDI...");

            var allContent = MigrationHelper.GetAllContent(_contentService).FilterBy(cliInput);

            MigrationHelper.SetBufferSize(allContent.Count);

            var contentCounter = 0;

            var totalCount = 0;
            var migratedCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var content in allContent)
            {
                contentCounter++;

                Console.WriteLine($"{contentCounter}/{allContent.Count}");

                foreach (var property in content.Properties.Where(x => x.PropertyType.PropertyEditorAlias == _newContentTypeAlias))
                {
                    ++totalCount;

                    if (property.Value != null)
                    {
                        string oldPropertyValue = property.Value.ToString();

                        try
                        {
                            bool migrated = TryMigrateProperyValue(oldPropertyValue, out var updatedValue);

                            if (migrated)
                            {
                                property.Value = updatedValue;

                                if (cliInput.ShouldPersist)
                                {
                                    _contentService.Save(content, raiseEvents: false);
                                }

                                ++migratedCount;

                                Console.ForegroundColor = ConsoleColor.Green;
                                MigrationHelper.WriteOutBoilerplate(content, property, oldPropertyValue);
                                Console.WriteLine($"Updated value: {updatedValue}");
                                Console.ResetColor();
                            }
                            else
                            {
                                ++skippedCount;
                            }
                        }
                        catch (Exception ex)
                        {
                            ++failedCount;

                            Console.ForegroundColor = ConsoleColor.Red;
                            MigrationHelper.WriteOutBoilerplate(content, property, oldPropertyValue);
                            Console.WriteLine($"Error occured, skipping. Details:\n{ex}");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        ++skippedCount;
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Finished, the results of properties data processing:");
            Console.WriteLine($"Total    : {totalCount}");
            Console.WriteLine($"Migrated : {migratedCount}");
            if (failedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine($"Failed   : {failedCount}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Skipped  : {skippedCount}");
            Console.ResetColor();

            return Task.FromResult(DeliverableResponse.Continue);
        }

        private bool TryMigrateProperyValue(string oldValue, out string newValue)
        {
            bool migrated = false;

            var links = JsonConvert.DeserializeObject<JArray>(oldValue);

            foreach (var rawLink in links)
            {
                if (rawLink is JObject link)
                {
                    var id = link.Value<int?>("id");

                    if (id != null)
                    {
                        var isMedia = link.Value<bool>("isMedia");
                        var udi = GetUdi(isMedia, id.Value);
                        if (udi != null)
                        {
                            link.Add("udi", udi.ToString());
                            link.Remove("id");
                            link.Remove("isMedia");
                            migrated = true;
                        }
                        else
                        {
                            var idType = isMedia ? "Media" : "Content";
                            throw new ApplicationException($"{idType} with id={id.Value} was not found.");
                        }
                    }

                    var anchorOrQuery = link.Value<string>("anchorOrQuery");
                    if (anchorOrQuery != null)
                    {
                        link.Add("queryString", anchorOrQuery);
                        link.Remove("anchorOrQuery");
                        migrated = true;
                    }
                }
            }

            if (migrated)
            {
                newValue = JsonConvert.SerializeObject(links, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            }
            else
            {
                newValue = null;
            }

            return migrated;
        }

        private Udi GetUdi(bool isMedia, int id)
        {
            return isMedia ? _mediaService.GetById(id)?.GetUdi() : _contentService.GetById(id)?.GetUdi();
        }
    }
}
