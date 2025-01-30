using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EMQ.Server;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CustomAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    public CustomAuthorizeAttribute(PermissionKind requiredPermission)
    {
        _requiredPermission = requiredPermission;
    }

    private readonly PermissionKind _requiredPermission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Session? session = null;
        if (context.HttpContext.Request.Headers.TryGetValue(AuthStuff.AuthorizationHeaderName, out var token))
        {
            session = ServerState.Sessions.FirstOrDefault(x => x.Token == token);
            context.HttpContext.Items["EMQ_SESSION"] = session;
        }

        bool valid = AuthStuff.HasPermission(session, _requiredPermission);
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
                // todo remove
                if (context.ActionDescriptor.DisplayName != null &&
                    !context.ActionDescriptor.DisplayName.Contains("SyncChat") &&
                    !context.ActionDescriptor.DisplayName.Contains("SyncRoom"))
                {
                    Console.WriteLine(
                        $"{ServerUtils.GetIpAddress(context.HttpContext)} failed {_requiredPermission} check for {context.ActionDescriptor.DisplayName}");
                }
            }

            context.Result = new UnauthorizedResult();
            return;
        }

        // Console.WriteLine(JsonSerializer.Serialize(, Utils.JsoIndented));
        await next();
    }
}
