using System;
using System.IO;
using System.Threading.Tasks;
using Chauffeur;
using Umbraco.Core.Services;

namespace Machina.Migrations
{
    [DeliverableName("machina-publish-root")]
    [DeliverableAlias("machina-proot")]
    public class RepublishAll : Deliverable
    {
        private readonly IContentService _contentService;

        public RepublishAll(
            TextReader reader,
            TextWriter writer,
            IContentService contentService
        ) : base(reader, writer)
        {
            _contentService = contentService;
        }

        public override Task<DeliverableResponse> Run(string command, string[] args)
        {
            Console.WriteLine("Run publish for all root content nodes and children ...");

            foreach (var rootContent in _contentService.GetRootContent())
            {
                Console.WriteLine($"Publishing {rootContent.Name} ...");
                _contentService.PublishWithChildrenWithStatus(rootContent);
            }

            Console.WriteLine("Done for all.");

            return Task.FromResult(DeliverableResponse.Continue);
        }
    }
}
