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
        HeaderPolicyCollection policyCollection = new HeaderPolicyCollection()
            /*
             * AddDefaultSecurityHeaders() adds the following industry-standard security headers:
             * - X-Frame-Options: Deny (Prevents the application from being loaded in an iframe, mitigating Clickjacking).
             * - X-Content-Type-Options: nosniff (Instructs the browser to strictly follow the Content-Type header and prevents MIME-type sniffing).
             * - X-XSS-Protection: 0 (Disables the legacy browser XSS filter, which is replaced by the more robust Content Security Policy).
             * - Referrer-Policy: strict-origin-when-cross-origin (Ensures that sensitive path/query info is not leaked when navigating to other domains).
             */
            .AddDefaultSecurityHeaders()
            /*
             * Content Security Policy (CSP) defines a "whitelist" of trusted content sources.
             * This helps prevent Cross-Site Scripting (XSS) and data injection attacks.
             */
            .AddContentSecurityPolicy(builder =>
            {
                // Disallow everything by default; explicit permissions must be added below.
                builder.AddDefaultSrc().None();

                // Allow the application to make fetch/XHR calls to its own domain.
                builder.AddConnectSrc().Self();

                // Allow loading fonts only from the application's own domain.
                builder.AddFontSrc().Self();

                // Allow images from self, data: URIs (common for icons), and Google Fonts CDN (required for Scalar UI).
                builder.AddImgSrc().Self().Data().From("https://fonts.gstatic.com");

                // Allow JavaScript execution only from the application's own domain.
                builder.AddScriptSrc().Self();

                /*
                 * Allow styles from self and 'unsafe-inline'.
                 * Note: 'unsafe-inline' is currently required because the Scalar API Reference UI
                 * injects styles directly into the document.
                 */
                builder.AddStyleSrc().Self().UnsafeInline();

                // Disallow the application from being embedded in any frames (alternative to X-Frame-Options).
                builder.AddFrameAncestors().None();
            })
            /*
             * Permissions-Policy controls which browser features (e.g., camera, microphone)
             * the application and its embedded content are allowed to access.
             * Setting these to () denies access to everyone.
             */
            .AddCustomHeader(
                "Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), interest-cohort=()"
            );

        return app.UseSecurityHeaders(policyCollection);
    }
}
