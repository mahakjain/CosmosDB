using AzureCosmosDB.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AzureCosmosDB.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly IDocumentDBService _documentDBService;
        private readonly string endPoint = "";
        private readonly string authorizationKey = "";

        public DatabaseController(IDocumentDBService documentDBService)
        {
            _documentDBService = documentDBService;
            _documentDBService.SetDatabase(endPoint, authorizationKey);
        }

        [ActionName("Index")]
        public async Task<IActionResult> Index()
        {
            return View(await _documentDBService.GetDatabaseCollections("hgApplicationDocDb"));
        }

        [ActionName("Migrate")]
        public async Task Migrate(string id)
        {
            await _documentDBService.MigrateCollection(id);
        }
    }
}