﻿using AllUp.DAL;
using AllUp.Helpers;
using AllUp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AllUp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public ProductsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;

        }
        public async Task<IActionResult> Index()
        {
            List<Product> products = await _db.Products
                .Include(x=>x.ProductImages)
                .Include(x=>x.ProductDetail)
                .Include(x=>x.Brand)
                .Include(x=>x.ProductTags)
                .ThenInclude(x=>x.Tag)
                .Include(x => x.ProductCategories)
                .ThenInclude(x=>x.Category)
                .ToListAsync();
            return View(products);
        }
        public async Task<IActionResult> Create()
        {
            ViewBag.Brands = await _db.Brands.ToListAsync();
            ViewBag.Tags = await _db.Tags.ToListAsync();
            ViewBag.MainCategories = await _db.Categories.Where(x => x.IsMain).ToListAsync();
            ViewBag.ChildCategories = (await _db.Categories.Include(x => x.Children).FirstOrDefaultAsync())?.Children;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, int brandId, int mainCategoryId, int? childCategoryId, int[] tagsId)
        {

            #region Get
            ViewBag.Brands = await _db.Brands.ToListAsync();
            ViewBag.Tags = await _db.Tags.ToListAsync();
            ViewBag.MainCategories = await _db.Categories.Where(x => x.IsMain).ToListAsync();
            ViewBag.ChildCategories = (await _db.Categories.Include(x => x.Children).FirstOrDefaultAsync())?.Children;
            #endregion

            #region IsExist
            bool isExist = await _db.Products.AnyAsync(x => x.Name == product.Name);
            if (isExist)
            {
                ModelState.AddModelError("Name", "This product is already exist!");
                return View();
            }
            #endregion

            #region Photos Save
            List<ProductImage> productImages = new();
            foreach (IFormFile photo in product.Photos)
            {
                if (photo == null)
                {
                    ModelState.AddModelError("Photo", "please select photo");
                    return View();
                }
                if (!photo.IsImage())
                {
                    ModelState.AddModelError("Photo", "please select image type");
                    return View();
                }
                if (photo.IsOrder1Mb())
                {
                    ModelState.AddModelError("Photo", "Max 1Mb");
                    return View();
                }
                string folder = Path.Combine(_env.WebRootPath, "assets", "images", "product");
                ProductImage productImage = new()
                {
                    Image = await photo.SaveFileAsync(folder)
                };
                productImages.Add(productImage);
            }
            product.ProductImages = productImages;
            #endregion

            #region Product Tags
            List<ProductTag> productTags = new();
            foreach (int tagId in tagsId)
            {
                ProductTag productTag = new()
                {
                    TagId = tagId,
                };
                productTags.Add(productTag);
            }
            product.ProductTags = productTags;
            #endregion

            #region Product Categories

            List<ProductCategory> productCategories = new List<ProductCategory>();
            ProductCategory mainProductCategory = new ProductCategory();
            mainProductCategory.CategoryId = mainCategoryId;
            productCategories.Add(mainProductCategory);
            if (childCategoryId != null)
            {
                ProductCategory childProductCategory = new ProductCategory();
                childProductCategory.CategoryId = (int)childCategoryId;
                productCategories.Add(childProductCategory);
            }
            product.ProductCategories = productCategories;

            #endregion

            product.BrandId = brandId;
            await _db.Products.AddAsync(product);
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }
        public async Task<IActionResult> LoadChildCategories(int mainCategoryId)
        {
            List<Category> childCategories = await _db.Categories.Where(x => x.ParentId == mainCategoryId).ToListAsync();
            return PartialView("_LoadChildCategoriesPartial", childCategories);
        }
    }
}
