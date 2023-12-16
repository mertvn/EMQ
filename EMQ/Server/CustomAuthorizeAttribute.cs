using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EMQ.Server;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CustomAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    // public CustomAuthorizeAttribute(params UserRoleKind[] validRoles)
    // {
    //     _validRoles = validRoles;
    // }
    //
    // public CustomAuthorizeAttribute(UserRoleKind minimumRole)
    // {
    //     _minimumRole = minimumRole;
    // }

    public CustomAuthorizeAttribute(PermissionKind requiredPermission)
    {
        _requiredPermission = requiredPermission;
    }

    // private readonly UserRoleKind[]? _validRoles;
    //
    // private readonly UserRoleKind? _minimumRole;
    //
    // private UserRoleKind Combined => _minimumRole ?? (UserRoleKind)_validRoles!.Sum(x => (int)x);

    private readonly PermissionKind _requiredPermission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Session? session = null;
        if (context.HttpContext.Request.Headers.TryGetValue(AuthStuff.AuthorizationHeaderName, out var token))
        {
            session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        }

        UserRoleKind currentRole = session?.UserRoleKind ?? UserRoleKind.Visitor;
        // Console.WriteLine($"checking if {currentRole} has permission {_requiredPermission} for {context.ActionDescriptor.DisplayName}");

        // if (_minimumRole != null)
        // {
        //     valid = session.UserRoleKind >= _minimumRole;
        // }
        // else if (_validRoles != null)
        // {
        //     if (_validRoles.Any(userRoleKind => session.UserRoleKind.HasFlag(userRoleKind)))
        //     {
        //         valid = true;
        //     }
        // }
        // else
        // {
        //     Console.WriteLine("invalid state at CustomAuthorizeAttribute");
        //     context.Result = new UnauthorizedResult();
        //     return;
        // }

        var allRoles = Enum.GetValues<UserRoleKind>();

        // List<UserRoleKind> userRoles =
        //     allRoles.Where(userRoleKind => session.UserRoleKind.HasFlag(userRoleKind)).ToList();

        List<UserRoleKind> userRoles = new();
        foreach (UserRoleKind userRoleKind in allRoles)
        {
            if (currentRole.HasFlag(userRoleKind))
            {
                userRoles.Add(userRoleKind);
            }
        }

        // var userPermissions = Session.DefaultRolePermissionsDict
        //     .Where(x => userRoles.Contains(x.Key))
        //     .SelectMany(y => y.Value).ToList();

        List<PermissionKind> userPermissions = new();
        foreach ((UserRoleKind key, PermissionKind[]? value) in AuthStuff.DefaultRolePermissionsDict)
        {
            if (userRoles.Contains(key))
            {
                userPermissions.AddRange(value);
            }
        }

        // Console.WriteLine($"userPermissions: {JsonSerializer.Serialize(userPermissions, Utils.JsoIndented)}");
        bool valid = userPermissions.Contains(_requiredPermission);
        if (valid)
        {
            bool requiresModeratorOrGreater = !AuthStuff.DefaultUserPermissions.Contains(_requiredPermission);
            if (requiresModeratorOrGreater)
            {
                if (session is not null)
                {
                    Console.WriteLine(
                        $"{session.UserRoleKind} {session.Player.Username} passed {_requiredPermission} check for {context.ActionDescriptor.DisplayName}");
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
        else
        {
            if (session is not null)
            {
                Console.WriteLine(
                    $"{session.Token} {session.UserRoleKind} p{session.Player.Id} {session.Player.Username} failed {_requiredPermission} check for {context.ActionDescriptor.DisplayName}");
            }
            else
            {
                Console.WriteLine(
                    $"{ServerUtils.GetIpAddress(context.HttpContext)} failed {_requiredPermission} check for {context.ActionDescriptor.DisplayName}");
            }

            context.Result = new UnauthorizedResult();
            return;
        }

        // Console.WriteLine(JsonSerializer.Serialize(, Utils.JsoIndented));
        await next();
    }
}
