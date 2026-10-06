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
    public Task<CartDto> GetMyCart() => _service.GetMyCartAsync();

    [HttpPost]
    public Task<CartDto> AddItem([FromBody] AddCartItemInput input) => _service.AddItemAsync(input);

    [HttpPut]
    public Task<CartDto> UpdateItem([FromBody] UpdateCartItemInput input) => _service.UpdateItemAsync(input);

    [HttpDelete]
    public Task<CartDto> DeleteItem([FromQuery] int id) => _service.DeleteItemAsync(id);
}
