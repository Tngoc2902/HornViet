using Website.Application.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Shopping;

/// <summary>UC Quản lý giỏ hàng (yêu cầu đăng nhập).</summary>
[Authorize]
public class CartController : WebsiteBaseController
{
    private readonly ICartAppService _service;

    public CartController(ICartAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<CartDto>> GetMyCart() => Run(() => _service.GetMyCartAsync());

    [HttpPost]
    public Task<ActionResult<CartDto>> AddItem([FromBody] AddCartItemInput input) => Run(() => _service.AddItemAsync(input));

    [HttpPut]
    public Task<ActionResult<CartDto>> UpdateItem([FromBody] UpdateCartItemInput input) => Run(() => _service.UpdateItemAsync(input));

    [HttpDelete]
    public Task<ActionResult<CartDto>> DeleteItem([FromQuery] int id) => Run(() => _service.DeleteItemAsync(id));
}
