using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using presentationSiteApi.Models;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return user;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        return await _db.Users.ToListAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, User updatedUser)
    {
        if (id != updatedUser.Id) return BadRequest();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;
        user.Message = updatedUser.Message;
        user.Phone = updatedUser.Phone;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}