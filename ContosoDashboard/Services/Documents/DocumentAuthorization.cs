using System.Security.Claims;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed class DocumentAuthorization : IDocumentAuthorization
{
    public bool CanView(Document document, ClaimsPrincipal user)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        if (document.UploaderId == userId.Value && document.LifecycleStatus != DocumentLifecycleStatus.Deleted)
        {
            return true;
        }

        return document.LifecycleStatus == DocumentLifecycleStatus.Active
            && document.ScanStatus == DocumentScanStatus.Clean
            && document.Visibility == DocumentVisibility.AuthorizedUsers;
    }

    public bool CanDownload(Document document, ClaimsPrincipal user) =>
        document.LifecycleStatus == DocumentLifecycleStatus.Active
        && document.ScanStatus == DocumentScanStatus.Clean
        && document.Visibility == DocumentVisibility.AuthorizedUsers
        && CanView(document, user);

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }
}
