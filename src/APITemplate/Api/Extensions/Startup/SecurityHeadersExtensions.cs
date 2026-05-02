using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders;
using SharedKernel.Application.Options.Http;

namespace APITemplate.Api.Extensions.Startup;

/// <summary>
/// Provides extension methods for configuring HTTP security headers in the ASP.NET Core request pipeline.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds a robust set of security headers to the application's middleware pipeline.
    /// This implementation follows OWASP and W3C security best practices to protect the API
    /// and its associated UI components (e.g., Scalar/Swagger) from common web vulnerabilities.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance with security headers applied.</returns>
    public static IApplicationBuilder UseSecurityHeadersPolicy(this IApplicationBuilder app)
    {
        IWebHostEnvironment environment =
            app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        ApiHstsOptions hstsOptions = app
            .ApplicationServices.GetRequiredService<IOptions<ApiHstsOptions>>()
            .Value;

        HeaderPolicyCollection policyCollection = new HeaderPolicyCollection()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()
            .AddXssProtectionDisabled()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin();

        if (!environment.IsDevelopment())
        {
            int maxAgeSeconds = (int)TimeSpan.FromDays(hstsOptions.MaxAgeDays).TotalSeconds;

            if (hstsOptions.Preload)
            {
                // If a dedicated Preload method isn't available, we can set the header manually.
                policyCollection.AddCustomHeader(
                    "Strict-Transport-Security",
                    $"max-age={maxAgeSeconds}; includeSubDomains; preload"
                );
            }
            else if (hstsOptions.IncludeSubDomains)
            {
                policyCollection.AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeSeconds);
            }
            else
            {
                policyCollection.AddStrictTransportSecurityMaxAge(maxAgeSeconds);
            }
        }

        /*
         * Content Security Policy (CSP) defines a "whitelist" of trusted content sources.
         * This helps prevent Cross-Site Scripting (XSS) and data injection attacks.
         */
        policyCollection.AddContentSecurityPolicy(builder =>
        {
            // Disallow everything by default; explicit permissions must be added below.
            builder.AddDefaultSrc().None();

            // Allow the application to make fetch/XHR calls to its own domain.
            builder.AddConnectSrc().Self();

            // Allow loading fonts from self and Google Fonts (required for Scalar UI).
            builder.AddFontSrc().Self().From("https://fonts.gstatic.com");

            // Allow images from self and data: URIs (common for icons).
            builder.AddImgSrc().Self().Data();

            // Allow JavaScript execution only from the application's own domain.
            builder.AddScriptSrc().Self();

            /*
             * Allow styles from self, 'unsafe-inline', and Google Fonts.
             * Note: 'unsafe-inline' is currently required because the Scalar API Reference UI
             * injects styles directly into the document.
             */
            builder.AddStyleSrc().Self().UnsafeInline().From("https://fonts.googleapis.com");

            // Disallow the application from being embedded in any frames (alternative to X-Frame-Options).
            builder.AddFrameAncestors().None();
        });

        /*
         * Permissions-Policy controls which browser features (e.g., camera, microphone)
         * the application and its embedded content are allowed to access.
         */
        policyCollection.AddPermissionsPolicy(builder =>
        {
            builder.AddCamera().None();
            builder.AddMicrophone().None();
            builder.AddGeolocation().None();
            builder.AddCustomFeature("interest-cohort").None();
        });

        return app.UseSecurityHeaders(policyCollection);
    }
}
