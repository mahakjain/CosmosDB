using AzureCosmosDB.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AzureCosmosDB.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly IDocumentDBService _documentDBService;
        private readonly string endPoint = "https://dev01-app-docdb.documents.azure.com:443/";
        private readonly string authorizationKey = "mMAHsZZwmmumo8GRkzz9K9VJh9gLItrr1Ah6Ys89hzL35qff8B7tGHBa6WMh9QckFMcSt1zwy9KVQJp3cgu5Tw==";

        public DatabaseController(IDocumentDBService documentDBService)
        {
            _documentDBService = documentDBService;
            _documentDBService.SetDatabase(endPoint, authorizationKey);
        }

        [ActionName("Index")]
        public async Task<IActionResult> Index()
        {
            return View(await _documentDBService.GetDatabaseCollections("hgApplicationDocDb").ConfigureAwait(false));
        }

        [ActionName("Migrate")]
        public async Task<IActionResult> Migrate(string id)
        {
            await _documentDBService.MigrateCollection(id).ConfigureAwait(false);
            
            return RedirectToAction("Index");
        }
    }
}