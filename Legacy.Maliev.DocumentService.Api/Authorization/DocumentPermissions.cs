namespace Legacy.Maliev.DocumentService.Api.Authorization;

/// <summary>Permissions required by authenticated document routes.</summary>
public static class DocumentPermissions
{
    /// <summary>Permission to render legacy document PDFs.</summary>
    public const string Render = "legacy.documents.render";
}