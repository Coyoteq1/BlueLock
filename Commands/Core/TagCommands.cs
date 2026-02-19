using System;
using System.Linq;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Services;
using VAuto.Zone.Core;

namespace VAuto.Commands.Core;

/// <summary>
/// Commands for managing player zone tags.
/// </summary>
[CommandGroup("tag")]
public static class ZoneTagCommands
{
    [Command("rename", "r", description: "Rename your zone tag.", adminOnly: false)]
    public static void RenameTagCommand(ChatCommandContext ctx, string newTag)
    {
        var steamId = ctx.User.PlatformId;
        
        if (!ZonePlayerTagService.HasTag(steamId))
        {
            ctx.Reply("You don't have an active zone tag. Enter a zone first.");
            return;
        }

        // Ensure tag format
        if (!newTag.StartsWith("[") && !newTag.EndsWith("]"))
        {
            newTag = $"[{newTag}]";
        }

        if (ZonePlayerTagService.RenameTag(steamId, newTag))
        {
            ctx.Reply($"Your tag has been changed to {newTag}");
        }
        else
        {
            ctx.Reply("Failed to rename your tag.");
        }
    }

    [Command("clear", description: "Clear your zone tag.", adminOnly: false)]
    public static void ClearTagCommand(ChatCommandContext ctx)
    {
        var steamId = ctx.User.PlatformId;
        
        if (!ZonePlayerTagService.HasTag(steamId))
        {
            ctx.Reply("You don't have an active zone tag.");
            return;
        }

        ZonePlayerTagService.RemoveTag(steamId, ctx.Event.SenderCharacterEntity, ZoneCore.EntityManager);
        ctx.Reply("Your zone tag has been cleared.");
    }

    [Command("list", "l", description: "List all active zone tags.", adminOnly: true)]
    public static void ListTagsCommand(ChatCommandContext ctx)
    {
        var tags = ZonePlayerTagService.GetAllTags();
        
        if (tags.Count == 0)
        {
            ctx.Reply("No active zone tags.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Active Zone Tags ({tags.Count}):");
        
        foreach (var tag in tags)
        {
            sb.AppendLine($"  {tag.Value.OriginalName}: {tag.Value.CurrentTag} (Zone: {tag.Value.ZoneId})");
        }

        ctx.Reply(sb.ToString());
    }

    [Command("set", description: "Set a player's zone tag (admin).", adminOnly: true)]
    public static void SetTagCommand(ChatCommandContext ctx, string playerName, string newTag)
    {
        // Find player by name
        var em = ZoneCore.EntityManager;
        var userQuery = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
        var users = userQuery.ToComponentDataArray<User>(Unity.Collections.Allocator.Temp);
        
        User targetUser = default;
        foreach (var user in users)
        {
            if (user.CharacterName.ToString().Equals(playerName, StringComparison.OrdinalIgnoreCase))
            {
                targetUser = user;
                break;
            }
        }
        users.Dispose();

        if (targetUser.PlatformId == 0)
        {
            ctx.Reply($"Player '{playerName}' not found.");
            return;
        }

        var steamId = targetUser.PlatformId;
        
        // Ensure tag format
        if (!newTag.StartsWith("[") && !newTag.EndsWith("]"))
        {
            newTag = $"[{newTag}]";
        }

        if (ZonePlayerTagService.RenameTag(steamId, newTag))
        {
            ctx.Reply($"Set {playerName}'s tag to {newTag}");
        }
        else
        {
            ctx.Reply($"Player {playerName} has no active zone tag.");
        }
    }
}
