
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Get only active categories
    public async Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            })
            .ToListAsync();
    }

    // Get active category by ID
    public async Task<CategoryResponseDto?> GetCategoryByIdAsync(int id)
    {
        return await _context.Categories
            .Where(c => c.Id == id && c.IsActive)
            .Select(c => new CategoryResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            })
            .FirstOrDefaultAsync();
    }

    // Create category
    public async Task<CategoryResponseDto?> CreateCategoryAsync(
        CreateCategoryDto dto)
    {
        var categoryName = dto.Name.Trim();

        var alreadyExists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == categoryName.ToLower());

        if (alreadyExists)
            return null;

        var category = new Category
        {
            Name = categoryName,
            Description = dto.Description,
            IsActive = true
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        };
    }

    // Update category
    public async Task<CategoryResponseDto?> UpdateCategoryAsync(
        int id,
        UpdateCategoryDto dto)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            return null;

        var categoryName = dto.Name.Trim();

        var alreadyExists = await _context.Categories
            .AnyAsync(c =>
                c.Id != id &&
                c.Name.ToLower() == categoryName.ToLower());

        if (alreadyExists)
            return null;

        category.Name = categoryName;
        category.Description = dto.Description;
        category.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        };
    }

    // Soft delete category
    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            return false;

        category.IsActive = false;

        await _context.SaveChangesAsync();

        return true;
    }
}
