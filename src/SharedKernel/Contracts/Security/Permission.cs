using System.Reflection;

namespace SharedKernel.Contracts.Security;

/// <summary>
///     Centralised registry of all fine-grained permission string constants used throughout the application.
///     Nested classes group permissions by domain resource; <see cref="All" /> enumerates every declared permission via
///     reflection.
/// </summary>
public static class Permission
{
    private static readonly Lazy<IReadOnlySet<string>> LazyAll = new(() =>
    {
        HashSet<string> permissions = new(StringComparer.Ordinal);
        foreach (
            Type nestedType in typeof(Permission).GetNestedTypes(
                BindingFlags.Public | BindingFlags.Static
            )
        )
        {
            foreach (
                FieldInfo field in nestedType.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
                )
            )
            {
                if (
                    field.IsLiteral
                    && field.FieldType == typeof(string)
                    && field.GetRawConstantValue() is string value
                )
                    permissions.Add(value);
            }
        }

        return permissions;
    });

    /// <summary>
    ///     Returns a lazily-initialised, read-only set containing every permission constant declared
    ///     across all nested resource classes, discovered via reflection.
    /// </summary>
    public static IReadOnlySet<string> All => LazyAll.Value;

    /// <summary>Permissions governing product resource access.</summary>
    public static class Products
    {
        public const string Read = "Products.Read";
        public const string Create = "Products.Create";
        public const string Update = "Products.Update";
        public const string Delete = "Products.Delete";
    }

    /// <summary>Permissions governing category resource access.</summary>
    public static class Categories
    {
        public const string Read = "Categories.Read";
        public const string Create = "Categories.Create";
        public const string Update = "Categories.Update";
        public const string Delete = "Categories.Delete";
    }

    /// <summary>Permissions governing product review resource access.</summary>
    public static class ProductReviews
    {
        public const string Read = "ProductReviews.Read";
        public const string Create = "ProductReviews.Create";
        public const string Delete = "ProductReviews.Delete";
    }

    /// <summary>Permissions governing supplementary product data resource access.</summary>
    public static class ProductData
    {
        public const string Read = "ProductData.Read";
        public const string Create = "ProductData.Create";
        public const string Delete = "ProductData.Delete";
    }

    /// <summary>Permissions governing user account resource access.</summary>
    public static class Users
    {
        public const string Read = "Users.Read";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
    }

    /// <summary>Permissions governing tenant resource access.</summary>
    public static class Tenants
    {
        public const string Read = "Tenants.Read";
        public const string Create = "Tenants.Create";
        public const string Delete = "Tenants.Delete";
    }

    /// <summary>Permissions governing tenant invitation resource access.</summary>
    public static class Invitations
    {
        public const string Read = "Invitations.Read";
        public const string Create = "Invitations.Create";
        public const string Revoke = "Invitations.Revoke";
    }

    /// <summary>Permissions governing example/showcase endpoint access.</summary>
    public static class Examples
    {
        public const string Read = "Examples.Read";
        public const string Create = "Examples.Create";
        public const string Update = "Examples.Update";
        public const string Execute = "Examples.Execute";
        public const string Upload = "Examples.Upload";
        public const string Download = "Examples.Download";
    }

    /// <summary>Permissions governing role access and management.</summary>
    public static class Roles
    {
        public const string Read = "Roles.Read";
        public const string Create = "Roles.Create";
        public const string Update = "Roles.Update";
        public const string Delete = "Roles.Delete";
    }

    /// <summary>Overarching global permissions.</summary>
    public static class Platform
    {
        public const string Manage = "Platform.Manage";
    }

    /// <summary>Overarching tenant permissions.</summary>
    public static class Tenant
    {
        public const string Manage = "Tenant.Manage";
    }
}
