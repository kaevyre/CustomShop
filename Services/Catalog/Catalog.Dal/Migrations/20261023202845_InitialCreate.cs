using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.CheckConstraint("CK_Brands_Website", "[Website] IS NULL OR [Website] LIKE N'https://%'");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_Slug", "[Slug] NOT LIKE N'%[^a-z0-9-]%'");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsCustomizable = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_BasePrice", "[BasePrice] > 0");
                    table.CheckConstraint("CK_Products_ProductType", "[ProductType] IN (N'Keyboard', N'Mouse', N'Headphones', N'Speakers', N'Mousepad', N'Accessory')");
                    table.CheckConstraint("CK_Products_Slug", "[Slug] NOT LIKE N'%[^a-z0-9-]%'");
                    table.CheckConstraint("CK_Products_Stock", "[Stock] >= 0");
                    table.ForeignKey(
                        name: "FK_Products_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomizationOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomizationOptions", x => x.Id);
                    table.CheckConstraint("CK_CustomizationOptions_PriceDelta", "[PriceDelta] >= 0");
                    table.ForeignKey(
                        name: "FK_CustomizationOptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => new { x.ProductId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProductCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecifications",
                columns: table => new
                {
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WeightGrams = table.Column<int>(type: "int", nullable: true),
                    Dimensions = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConnectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Material = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: false, defaultValue: 12)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecifications", x => x.ProductId);
                    table.CheckConstraint("CK_ProductSpecifications_Connection", "[ConnectionType] IN (N'Wired', N'Wireless', N'Bluetooth', N'Hybrid', N'None')");
                    table.CheckConstraint("CK_ProductSpecifications_Warranty", "[WarrantyMonths] BETWEEN 0 AND 60");
                    table.CheckConstraint("CK_ProductSpecifications_Weight", "[WeightGrams] > 0");
                    table.ForeignKey(
                        name: "FK_ProductSpecifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Id", "Country", "CreatedAt", "CreatedBy", "Name", "UpdatedAt", "UpdatedBy", "Website" },
                values: new object[,]
                {
                    { 1, "Україна", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Keyforge", null, null, "https://keyforge.example.com" },
                    { 2, "Польща", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Glidex", null, null, "https://glidex.example.com" },
                    { 3, "Україна", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "SonicCraft", null, null, "https://soniccraft.example.com" },
                    { 4, "Україна", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "PadArt", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Name", "Slug", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Клавіатури", "keyboards", 1, null, null },
                    { 2, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Миші", "mice", 2, null, null },
                    { 3, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Аудіо", "audio", 3, null, null },
                    { 4, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Килимки", "mousepads", 4, null, null },
                    { 5, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Аксесуари", "accessories", 5, null, null },
                    { 6, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Кастомізація", "custom", 6, null, null },
                    { 7, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Новинки", "new", 7, null, null }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BasePrice", "BrandId", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsCustomizable", "Name", "ProductType", "Sku", "Slug", "Stock", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1L, 4500.00m, 1, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Механічна клавіатура 75% в алюмінієвому корпусі, gasket mount", true, true, "Клавіатура Aurora 75 (алюміній)", "Keyboard", "KB-075-ALU", "aurora-75-alu", 15, null, null },
                    { 2L, 2200.00m, 2, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Легка ігрова миша 58 г, сенсор 26K DPI", true, true, "Миша Glide Pro", "Mouse", "MS-GLD-01", "glide-pro", 30, null, null },
                    { 3L, 3800.00m, 3, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Закриті студійні навушники зі змінними амбушурами", true, true, "Навушники Studio Wave", "Headphones", "HP-STU-02", "studio-wave", 12, null, null }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BasePrice", "BrandId", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "ProductType", "Sku", "Slug", "Stock", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 4L, 3100.00m, 3, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Настільна акустика 2.0, 2×15 Вт", true, "Колонки DeskBeat 2.0", "Speakers", "SP-DSK-20", "deskbeat-20", 10, null, null });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BasePrice", "BrandId", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsCustomizable", "Name", "ProductType", "Sku", "Slug", "Stock", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 5L, 950.00m, 4, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Тканинний килимок з прошитим краєм, можливий власний принт", true, true, "Килимок Canvas XL 900×400", "Mousepad", "MP-XL-900", "canvas-xl-900", 50, null, null });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BasePrice", "BrandId", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "ProductType", "Sku", "Slug", "Stock", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 6L, 1600.00m, 1, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Набір 140 кейкапів PBT, профіль Cherry", true, "Набір кейкапів PBT Retro", "Accessory", "KC-PBT-RETRO", "pbt-retro-keycaps", 25, null, null });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BasePrice", "BrandId", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsCustomizable", "Name", "ProductType", "Sku", "Slug", "Stock", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 7L, 700.00m, 1, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Кручений кабель з авіаційним роз'ємом", true, true, "Кабель Coil USB-C", "Accessory", "CB-COIL-USBC", "coil-usb-c", 40, null, null },
                    { 8L, 2900.00m, 1, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Компактна 60% клавіатура з hot-swap роз'ємами", true, true, "Клавіатура Pocket 60 Hot-Swap", "Keyboard", "KB-060-HS", "pocket-60-hs", 20, null, null }
                });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 1L, "Світчі", true, true, 1L, 1, "Gateron Red" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 2L, "Світчі", true, 900.00m, 1L, 2, "Holy Panda" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 3L, "Кейкапи", true, true, 1L, 1, "PBT Black" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 4L, "Кейкапи", true, 600.00m, 1L, 2, "Retro Beige" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 5L, "Колір корпусу", true, true, 1L, 1, "Silver" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 6L, "Колір корпусу", true, 400.00m, 1L, 2, "Midnight Blue" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 7L, "Колір корпусу", true, true, 2L, 1, "Black" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 8L, "Колір корпусу", true, 200.00m, 2L, 2, "Lilac" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 9L, "Покриття", true, true, 2L, 1, "Matte" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 10L, "Покриття", true, 150.00m, 2L, 2, "Glossy" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 11L, "Амбушури", true, true, 3L, 1, "Velour" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[,]
                {
                    { 12L, "Амбушури", true, 450.00m, 3L, 2, "Leather" },
                    { 13L, "Гравіювання", true, 300.00m, 3L, 1, "Так" }
                });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 14L, "Принт", true, true, 5L, 1, "Без принту" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 15L, "Принт", true, 350.00m, 5L, 2, "Власний принт" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 16L, "Світчі", true, true, 8L, 1, "Gateron Yellow" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 17L, "Світчі", true, 500.00m, 8L, 2, "Kailh Box White" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "IsDefault", "ProductId", "SortOrder", "Value" },
                values: new object[] { 18L, "Довжина", true, true, 7L, 1, "1.5 м" });

            migrationBuilder.InsertData(
                table: "CustomizationOptions",
                columns: new[] { "Id", "GroupName", "IsAvailable", "PriceDelta", "ProductId", "SortOrder", "Value" },
                values: new object[] { 19L, "Довжина", true, 100.00m, 7L, 2, "2 м" });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomizationOptions_DefaultPerGroup",
                table: "CustomizationOptions",
                columns: new[] { "ProductId", "GroupName" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomizationOptions_ProductId_GroupName_Value",
                table: "CustomizationOptions",
                columns: new[] { "ProductId", "GroupName", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_CategoryId_AddedAt",
                table: "ProductCategories",
                columns: new[] { "CategoryId", "AddedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name",
                filter: "[IsActive] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Slug", "BasePrice" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductType_BasePrice",
                table: "Products",
                columns: new[] { "ProductType", "BasePrice" },
                filter: "[IsActive] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Name", "Slug", "BrandId", "IsCustomizable" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomizationOptions");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "ProductSpecifications");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
