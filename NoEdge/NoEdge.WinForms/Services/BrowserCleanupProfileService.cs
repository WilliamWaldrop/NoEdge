using System;
using System.Collections.Generic;
using System.Linq;

namespace NoEdge.WinForms.Services;

public sealed class BrowserCleanupProfileService
{
    private static readonly IReadOnlyList<BrowserCleanupProfile> Profiles =
        new List<BrowserCleanupProfile>
        {
            new(
                "Microsoft Edge",
                "Edge",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                "Reduces selected background, startup, sidebar, and recommendation behavior while preserving Edge, WebView2, browser updates, and user data.",
                new List<BrowserPolicySetting>
                {
                    new("BackgroundModeEnabled", 0),
                    new("StartupBoostEnabled", 0),
                    new("HubsSidebarEnabled", 0),
                    new("ShowRecommendationsEnabled", 0)
                }
            ),

            new(
                "Google Chrome",
                "Chrome",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Google\Chrome",
                "Reduces selected background behavior and promotional-tab behavior while preserving Chrome, updates, and user data.",
                new List<BrowserPolicySetting>
                {
                    new("BackgroundModeEnabled", 0),
                    new("PromotionalTabsEnabled", 0)
                }
            ),

            new(
                "Brave",
                "Brave",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\BraveSoftware\Brave",
                "Reduces selected background behavior and promotional-tab behavior while preserving Brave, updates, and user data.",
                new List<BrowserPolicySetting>
                {
                    new("BackgroundModeEnabled", 0),
                    new("PromotionalTabsEnabled", 0)
                }
            )
        };

    public IReadOnlyList<BrowserCleanupProfile> GetAll()
    {
        return Profiles;
    }

    public BrowserCleanupProfile? GetById(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return Profiles.FirstOrDefault(profile =>
            profile.Id.Equals(
                profileId,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }
}

public sealed record BrowserCleanupProfile(
    string BrowserName,
    string Id,
    string RegistryPath,
    string Description,
    IReadOnlyList<BrowserPolicySetting> Settings
);

public sealed record BrowserPolicySetting(
    string Name,
    int Value
);