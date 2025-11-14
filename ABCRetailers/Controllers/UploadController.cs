using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ABCRetailers.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IAzureStorageService storageService, ILogger<UploadController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                    {
                        // Validate file size (10MB max)
                        if (model.ProofOfPayment.Length > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError("ProofOfPayment", "File size must be less than 10MB");
                            return View(model);
                        }

                        // Validate file type
                        var validTypes = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var fileExtension = Path.GetExtension(model.ProofOfPayment.FileName).ToLower();
                        if (!validTypes.Contains(fileExtension))
                        {
                            ModelState.AddModelError("ProofOfPayment", "Please select a valid file type (PDF, JPG, PNG, DOC, DOCX)");
                            return View(model);
                        }

                        // Upload to blob storage (this will always work)
                        var fileName = await _storageService.UploadFileAsync(model.ProofOfPayment, "payment-proofs");
                        _logger.LogInformation($"✅ File uploaded to blob storage: {fileName}");

                        // Try to upload to file share (might not work in development storage)
                        try
                        {
                            var fileShareResult = await _storageService.UploadToFileShareAsync(model.ProofOfPayment, "contracts", "payments");

                            if (string.IsNullOrEmpty(fileShareResult))
                            {
                                _logger.LogInformation("ℹ️ File share upload skipped (development storage mode)");
                                TempData["Success"] = $"File '{model.ProofOfPayment.FileName}' uploaded to cloud storage successfully! (File shares not available in development mode)";
                            }
                            else
                            {
                                _logger.LogInformation($"✅ File also uploaded to file share: {fileShareResult}");
                                TempData["Success"] = $"File '{model.ProofOfPayment.FileName}' uploaded successfully to both storage systems!";
                            }
                        }
                        catch (Exception shareEx)
                        {
                            // Log but don't fail - blob storage is the primary
                            _logger.LogWarning(shareEx, "File share upload failed, but blob storage succeeded");
                            TempData["Success"] = $"File '{model.ProofOfPayment.FileName}' uploaded to cloud storage successfully! (File share backup skipped)";
                        }

                        return View(new FileUploadModel());
                    }
                    else
                    {
                        ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file");
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");

                    // More specific error message for the user
                    if (ex.Message.Contains("development storage", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("", "Storage services are currently in development mode. Some features may be limited.");
                    }
                }
            }

            return View(model);
        }
    }
}