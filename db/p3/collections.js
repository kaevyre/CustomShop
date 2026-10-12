// Проєкт №3 — контекст «Відгуки та галерея» (MongoDB), БД customshop_reviews.
// Створює 4 колекції з JSON Schema Validation та індекси; повторний запуск безпечний
// (для наявної колекції валідатор оновлюється через collMod).
// _id — ObjectId: генерується клієнтом без центрального лічильника.

db = db.getSiblingDB("customshop_reviews");

function ensureCollection(name, schema) {
  const options = {
    validator: { $jsonSchema: schema },
    validationLevel: "strict",
    validationAction: "error",
  };
  if (db.getCollectionNames().includes(name)) {
    db.runCommand({ collMod: name, ...options });
    print(`collection '${name}': validator updated`);
  } else {
    db.createCollection(name, options);
    print(`collection '${name}': created`);
  }
}

// userProfiles: customerId — Id клієнта в БД Замовлень (посилання без FK).
ensureCollection("userProfiles", {
  bsonType: "object",
  required: ["customerId", "displayName", "joinedAt"],
  properties: {
    customerId:  { bsonType: ["int", "long"], description: "Id у БД Замовлень" },
    displayName: { bsonType: "string", minLength: 2, maxLength: 60 },
    avatarUrl:   { bsonType: "string" },
    bio:         { bsonType: "string", maxLength: 500 },
    socials:     { bsonType: "object" },
    favoriteSwitches: { bsonType: "array", items: { bsonType: "string" } },
    stats: {
      bsonType: "object",
      properties: {
        reviewsCount: { bsonType: "int", minimum: 0 },
        buildsCount:  { bsonType: "int", minimum: 0 },
      },
    },
    joinedAt: { bsonType: "date" },
  },
});

// reviews
// Embedding:   author, comments[], customization
// Referencing: productId -> Каталог, orderId -> Замовлення, discussionId -> discussions, author.profileId -> userProfiles
// Денормалізація: productName, productType, author.displayName
ensureCollection("reviews", {
  bsonType: "object",
  required: ["productId", "productName", "author", "rating", "text", "createdAt"],
  properties: {
    productId:   { bsonType: ["int", "long"], description: "Id товару в Каталозі (без FK)" },
    productName: { bsonType: "string", description: "денормалізовано з Каталогу" },
    productType: { enum: ["Keyboard", "Mouse", "Headphones", "Speakers", "Mousepad", "Accessory"] },
    author: {
      bsonType: "object",
      required: ["profileId", "displayName"],
      properties: {
        profileId:   { bsonType: "objectId" },
        displayName: { bsonType: "string" },
      },
    },
    rating: { bsonType: "int", minimum: 1, maximum: 5 },
    title:  { bsonType: "string", maxLength: 120 },
    text:   { bsonType: "string", minLength: 3, maxLength: 5000 },
    pros:   { bsonType: "array", items: { bsonType: "string" } },
    cons:   { bsonType: "array", items: { bsonType: "string" } },
    photos: { bsonType: "array", items: { bsonType: "string" } },
    customization: { bsonType: "object" },
    verifiedPurchase: { bsonType: "bool" },
    orderId: { bsonType: ["int", "long"], description: "Id замовлення в БД Замовлень (без FK)" },
    helpfulVotes: { bsonType: "int", minimum: 0 },
    comments: {
      bsonType: "array",
      maxItems: 50,                                   // більше — переносимо в discussions
      items: {
        bsonType: "object",
        required: ["author", "text", "createdAt"],
        properties: {
          author:    { bsonType: "string" },
          text:      { bsonType: "string", minLength: 1 },
          createdAt: { bsonType: "date" },
        },
      },
    },
    discussionId: { bsonType: "objectId" },
    createdAt: { bsonType: "date" },
    updatedAt: { bsonType: "date" },
  },
});

// discussions: довгі обговорення винесено окремо і пов'язано з reviews посиланням (reviewId);
// повідомлення вбудовані, автор — profileId + знімок імені.
ensureCollection("discussions", {
  bsonType: "object",
  required: ["reviewId", "productId", "title", "messages", "messageCount", "lastMessageAt"],
  properties: {
    reviewId:  { bsonType: "objectId" },
    productId: { bsonType: ["int", "long"] },
    title:     { bsonType: "string" },
    isClosed:  { bsonType: "bool" },
    messageCount:  { bsonType: "int", minimum: 0 },
    lastMessageAt: { bsonType: "date" },
    messages: {
      bsonType: "array",
      items: {
        bsonType: "object",
        required: ["author", "text", "createdAt"],
        properties: {
          author: {
            bsonType: "object",
            required: ["displayName"],
            properties: { profileId: { bsonType: "objectId" }, displayName: { bsonType: "string" } },
          },
          text:      { bsonType: "string" },
          replyTo:   { bsonType: "int" },
          createdAt: { bsonType: "date" },
        },
      },
    },
  },
});

// builds: items[] — товари сетапу, у кожного свій набір налаштувань; productName — ЗНІМОК на момент публікації.
ensureCollection("builds", {
  bsonType: "object",
  required: ["title", "author", "items", "createdAt"],
  properties: {
    title:  { bsonType: "string", minLength: 3, maxLength: 120 },
    description: { bsonType: "string" },
    author: {
      bsonType: "object",
      required: ["profileId", "displayName"],
      properties: { profileId: { bsonType: "objectId" }, displayName: { bsonType: "string" } },
    },
    items: {
      bsonType: "array",
      minItems: 1,
      items: {
        bsonType: "object",
        required: ["productId", "productName", "productType"],
        properties: {
          productId:     { bsonType: ["int", "long"] },
          productName:   { bsonType: "string" },
          productType:   { bsonType: "string" },
          customization: { bsonType: "object" },
          note:          { bsonType: "string" },
        },
      },
    },
    tags:   { bsonType: "array", items: { bsonType: "string" } },
    photos: { bsonType: "array", items: { bsonType: "string" } },
    likes:  { bsonType: "int", minimum: 0 },
    createdAt: { bsonType: "date" },
  },
});

// ІНДЕКСИ (над кожним — запит, під який його створено)

// userProfiles: знайти профіль за Id клієнта з БД Замовлень (обробка подій)
db.userProfiles.createIndex({ customerId: 1 }, { unique: true, name: "ux_customerId" });

// reviews: сторінка товару — «відгуки товару X, нові першими»
db.reviews.createIndex({ productId: 1, createdAt: -1 }, { name: "ix_product_createdAt" });

// reviews: профіль — «усі відгуки автора»
db.reviews.createIndex({ "author.profileId": 1, createdAt: -1 }, { name: "ix_author_createdAt" });

// reviews: один автор — один відгук на товар
db.reviews.createIndex({ productId: 1, "author.profileId": 1 }, { unique: true, name: "ux_product_author" });

// reviews: повнотекстовий пошук по відгуках
db.reviews.createIndex(
  { title: "text", text: "text" },
  { name: "tx_title_text", default_language: "none" }   // "none": без стемінгу, бо української мови немає в списку
);

// discussions: відкрити обговорення відгуку (одне на відгук)
db.discussions.createIndex({ reviewId: 1 }, { unique: true, name: "ux_reviewId" });

// discussions: стрічка «активні обговорення» — останні повідомлення першими
db.discussions.createIndex({ lastMessageAt: -1 }, { name: "ix_lastMessageAt" });

// builds: «сетапи, де використано товар X» (multikey по масиву items)
db.builds.createIndex({ "items.productId": 1 }, { name: "ix_items_productId" });

// builds: фільтр за тегом, найпопулярніші першими
db.builds.createIndex({ tags: 1, likes: -1 }, { name: "ix_tags_likes" });

print("collections.js: done");
