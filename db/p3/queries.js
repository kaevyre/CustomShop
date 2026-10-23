// Проєкт №3 — запити, агрегації та обробники подій. Розділи 1–5, 7, 8 лише читають дані;
// розділ 6 — обробники подій політики дублювання (виклики закоментовано).

db = db.getSiblingDB("customshop_reviews");

function show(title, cursorOrArray) {
  print(`\n===== ${title} =====`);
  printjson(Array.isArray(cursorOrArray) ? cursorOrArray : cursorOrArray.toArray());
}

// ---------- 1. Гнучка схема: документи з різними наборами полів ----------
// Самоперевірка: db.reviews.find() повертає документи різної структури.
show("reviews: набір полів кожного документа", db.reviews.aggregate([
  { $project: { _id: 0, productName: 1, fields: { $map: { input: { $objectToArray: "$$ROOT" }, in: "$$this.k" } } } },
]));

show("builds: різні налаштування в items", db.builds.aggregate([
  { $unwind: "$items" },
  { $project: { _id: 0, build: "$title", product: "$items.productName",
                customizationKeys: { $map: { input: { $objectToArray: { $ifNull: ["$items.customization", {}] } }, in: "$$this.k" } } } },
]));

// ---------- 2. CRUD / пошук / пагінація ----------
// відгуки товару 1, нові першими, сторінка 1 по 2 (ix_product_createdAt)
const pageSize = 2, page = 1;
show("відгуки товару 1 (сторінка 1)", db.reviews
  .find({ productId: 1 }, { productName: 1, "author.displayName": 1, rating: 1, createdAt: 1 })
  .sort({ createdAt: -1 })
  .skip((page - 1) * pageSize)
  .limit(pageSize));

// повнотекстовий пошук (tx_title_text)
show("пошук «корпус»", db.reviews.find(
  { $text: { $search: "корпус" } },
  { productName: 1, title: 1 }
));

// сетапи, де використано товар 1 (ix_items_productId)
show("сетапи з товаром 1", db.builds.find({ "items.productId": 1 }, { title: 1, likes: 1 }));

// лише підтверджені покупки з фото
show("verified + photos", db.reviews.find(
  { verifiedPurchase: true, photos: { $exists: true, $ne: [] } },
  { productName: 1, rating: 1, photos: 1 }
));

// ---------- 3. Агрегація: рейтинг товарів із розподілом оцінок ----------
show("рейтинг за товарами", db.reviews.aggregate([
  { $group: {
      _id: "$productId",
      productName: { $first: "$productName" },
      avgRating:   { $avg: "$rating" },
      reviews:     { $sum: 1 },
      verified:    { $sum: { $cond: ["$verifiedPurchase", 1, 0] } },
      fiveStars:   { $sum: { $cond: [{ $eq: ["$rating", 5] }, 1, 0] } },
  } },
  { $project: { productName: 1, reviews: 1, verified: 1, fiveStars: 1, avgRating: { $round: ["$avgRating", 2] } } },
  { $sort: { avgRating: -1, reviews: -1 } },
]));

// ---------- 4. Агрегація: найпопулярніші товари в сетапах ----------
show("товари в сетапах", db.builds.aggregate([
  { $unwind: "$items" },
  { $group: { _id: "$items.productId", productName: { $first: "$items.productName" },
              usedInBuilds: { $sum: 1 }, totalLikes: { $sum: "$likes" } } },
  { $sort: { usedInBuilds: -1, totalLikes: -1 } },
]));

// ---------- 5. $lookup: відгук + обговорення (reference) + профіль автора ----------
show("відгук з обговоренням і профілем", db.reviews.aggregate([
  { $match: { discussionId: { $exists: true } } },
  { $lookup: { from: "discussions", localField: "discussionId", foreignField: "_id", as: "discussion" } },
  { $lookup: { from: "userProfiles", localField: "author.profileId", foreignField: "_id", as: "profile" } },
  { $project: {
      productName: 1, rating: 1, commentsCount: { $size: { $ifNull: ["$comments", []] } },
      discussionTitle: { $first: "$discussion.title" },
      discussionMessages: { $first: "$discussion.messageCount" },
      authorSocials: { $first: "$profile.socials" },
  } },
]));

// ---------- 6. Обробники подій (політика дублювання) ----------

// Подія ProductUpdated з Каталогу: оновити productName у відгуках і обговореннях.
// builds НЕ оновлюємо: там назва — знімок на момент публікації.
function onProductRenamed(productId, newName) {
  const r = db.reviews.updateMany({ productId: productId }, { $set: { productName: newName, updatedAt: new Date() } });
  print(`reviews updated: ${r.modifiedCount}`);
}

// Подія OrderShipped із Замовлень: позначити відгуки покупця на ці товари як підтверджені.
function onOrderShipped(orderId, customerId, productIds) {
  const profile = db.userProfiles.findOne({ customerId: customerId }, { _id: 1 });
  if (!profile) return;
  const r = db.reviews.updateMany(
    { "author.profileId": profile._id, productId: { $in: productIds } },
    { $set: { verifiedPurchase: true, orderId: orderId, updatedAt: new Date() } }
  );
  print(`reviews verified: ${r.modifiedCount}`);
}

// Зміна displayName у профілі (дані цієї ж БД): оновити денормалізовані копії.
function onProfileRenamed(profileId, newName) {
  db.userProfiles.updateOne({ _id: profileId }, { $set: { displayName: newName } });
  db.reviews.updateMany({ "author.profileId": profileId }, { $set: { "author.displayName": newName } });
  db.builds.updateMany({ "author.profileId": profileId }, { $set: { "author.displayName": newName } });
  db.discussions.updateMany(
    { "messages.author.profileId": profileId },
    { $set: { "messages.$[m].author.displayName": newName } },
    { arrayFilters: [{ "m.author.profileId": profileId }] }
  );
}

// Приклади виклику (змінюють дані, тому закоментовані):
// onProductRenamed(NumberInt(1), "Клавіатура Aurora 75 Pro (алюміній)");
// onOrderShipped(NumberInt(3), NumberInt(3), [NumberInt(3)]);
// onProfileRenamed(ObjectId("66f0c2a1e4b0a1b2c3d40002"), "Andrii Melnyk");

// ---------- 7. Валідація: некоректний документ відхиляється ----------
try {
  db.reviews.insertOne({ productId: NumberInt(2), productName: "Миша Glide Pro",
                         author: { profileId: ObjectId(), displayName: "x" },
                         rating: NumberInt(9), text: "Оцінка 9 з 5", createdAt: new Date() });
} catch (e) {
  print(`\nвалідація спрацювала: ${e.message.split("\n")[0]}`);
}

// ---------- 8. Індекси ----------
["userProfiles", "reviews", "discussions", "builds"].forEach(c =>
  show(`індекси ${c}`, db.getCollection(c).getIndexes().map(i => ({ name: i.name, key: i.key, unique: !!i.unique }))));
