namespace Legacy.Maliev.DocumentService.Api.Authorization;

/// <summary>Permissions required by the authenticated document rendering routes.</summary>
public static class DocumentPermissions
{
    /// <summary>Allows rendering the five supported legacy document types.</summary>
    public const string Render = "legacy.documents.render";
}
