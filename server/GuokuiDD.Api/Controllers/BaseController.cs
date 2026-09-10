using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace GuokuiDD.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected int Uid() => int.Parse(User.FindFirstValue("uid")!);
}
