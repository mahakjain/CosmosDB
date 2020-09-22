using AzureCosmosDB.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AzureCosmosDB.Controllers
{
    public class UserDatabaseController : Controller
    {
        private readonly IDocumentDBService _documentDBService;
        private readonly string endPoint = "";
        private readonly string authorizationKey = "";
        private readonly string databaseName = "hgUserDocDb";

        public UserDatabaseController(IDocumentDBService documentDBService)
        {
            _documentDBService = documentDBService;
            _documentDBService.SetDatabase(endPoint, authorizationKey);
        }

        [ActionName("Index")]
        public async Task<IActionResult> Index()
        {
            return View(await _documentDBService.GetDatabaseCollections(databaseName).ConfigureAwait(false));
        }

        [ActionName("MigrateToTemp")]
        public async Task<IActionResult> MigrateToTemp(string id)
        {
            await _documentDBService.MigrateToTempCollection(id).ConfigureAwait(false);

            return RedirectToAction("Index");
        }

        [ActionName("MigrateToPartition")]
        public async Task<IActionResult> MigrateToPartition(string id)
        {
            await _documentDBService.DeleteAndMigrateCollection(id).ConfigureAwait(false);

            return RedirectToAction("Index");
        }
    }
}