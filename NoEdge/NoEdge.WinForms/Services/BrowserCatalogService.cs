using System;
using System.Collections.Generic;
using System.Linq;

namespace NoEdge.WinForms.Services;

public sealed class BrowserCatalogService
{
    private static readonly IReadOnlyList<BrowserCatalogItem> Browsers =
        new List<BrowserCatalogItem>
        {
            new(
                "Firefox",
                "Mozilla.Firefox",
                "Mozilla",
                "Firefox stable browser"
            ),

            new(
                "LibreWolf",
                "LibreWolf.LibreWolf",
                "LibreWolf Community",
                "Privacy-focused Firefox-based browser"
            ),

            new(
                "Brave",
                "Brave.Brave",
                "Brave Software",
                "Brave stable browser"
            ),

            new(
                "Google Chrome",
                "Google.Chrome",
                "Google",
                "Google Chrome stable browser"
            ),

            new(
                "Chromium",
                "Hibbiki.Chromium",
                "Chromium",
                "Open-source Chromium browser"
            ),

            new(
                "Vivaldi",
                "Vivaldi.Vivaldi",
                "Vivaldi Technologies",
                "Vivaldi stable browser"
            ),

            new(
                "Opera",
                "Opera.Opera",
                "Opera Software",
                "Opera stable browser"
            )
        };

    public IReadOnlyList<BrowserCatalogItem> GetAll()
    {
        return Browsers;
    }

    public BrowserCatalogItem? GetByPackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return null;
        }

        return Browsers.FirstOrDefault(browser =>
            browser.PackageId.Equals(
                packageId,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    public BrowserCatalogItem? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Browsers.FirstOrDefault(browser =>
            browser.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }
}

public sealed record BrowserCatalogItem(
    string Name,
    string PackageId,
    string Publisher,
    string Description
);