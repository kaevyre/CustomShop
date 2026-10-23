// Проєкт №3 — тестові дані (запускати після collections.js).
// Ідемпотентний: фіксований _id + updateOne($setOnInsert, upsert) — наявні документи не змінюються.
// Числа, які валідатор вимагає як int, передаються через NumberInt().

db = db.getSiblingDB("customshop_reviews");

function seed(collection, docs) {
  let inserted = 0;
  for (const doc of docs) {
    const { _id, ...rest } = doc;
    const res = db.getCollection(collection).updateOne(
      { _id },
      { $setOnInsert: rest },
      { upsert: true }
    );
    if (res.upsertedCount === 1) inserted++;
  }
  print(`${collection}: inserted ${inserted}, total ${db.getCollection(collection).countDocuments()}`);
}

const P = {
  olena:  ObjectId("66f0c2a1e4b0a1b2c3d40001"),
  andrii: ObjectId("66f0c2a1e4b0a1b2c3d40002"),
  maria:  ObjectId("66f0c2a1e4b0a1b2c3d40003"),
  taras:  ObjectId("66f0c2a1e4b0a1b2c3d40004"),
};
const R = {
  auroraOlena: ObjectId("66f0c2a1e4b0a1b2c3d41001"),
  canvasOlena: ObjectId("66f0c2a1e4b0a1b2c3d41002"),
  pocketAndrii: ObjectId("66f0c2a1e4b0a1b2c3d41003"),
  deskbeatMaria: ObjectId("66f0c2a1e4b0a1b2c3d41004"),
  auroraTaras: ObjectId("66f0c2a1e4b0a1b2c3d41005"),
};
const D1 = ObjectId("66f0c2a1e4b0a1b2c3d42001");

seed("userProfiles", [
  {
    _id: P.olena,
    customerId: NumberInt(1),
    displayName: "Олена К.",
    avatarUrl: "avatars/olena.jpg",
    bio: "Збираю клавіатури в ретро-стилі",
    socials: { instagram: "@olena.keys" },
    favoriteSwitches: ["Holy Panda", "Gateron Red"],
    stats: { reviewsCount: NumberInt(2), buildsCount: NumberInt(1) },
    joinedAt: new Date("2026-08-30T09:00:00Z"),
  },
  {
    _id: P.andrii,
    customerId: NumberInt(2),
    displayName: "Andrii_M",
    socials: { youtube: "@andrii-desk", twitch: "andrii_m" },
    stats: { reviewsCount: NumberInt(1), buildsCount: NumberInt(1) },
    joinedAt: new Date("2026-09-19T12:00:00Z"),
  },
  {
    _id: P.maria,
    customerId: NumberInt(3),
    displayName: "Марія",
    joinedAt: new Date("2026-10-01T15:30:00Z"),
  },
  {
    _id: P.taras,
    customerId: NumberInt(4),
    displayName: "Тарас Б.",
    avatarUrl: "avatars/taras.png",
    joinedAt: new Date("2026-09-25T18:00:00Z"),
  },
]);

seed("reviews", [
  {
    _id: R.auroraOlena,
    productId: NumberInt(1),
    productName: "Клавіатура Aurora 75 (алюміній)",
    productType: "Keyboard",
    author: { profileId: P.olena, displayName: "Олена К." },
    rating: NumberInt(5),
    title: "Найкраща клавіатура, яку я мала",
    text: "Корпус важкий і не дзвенить, Holy Panda відчуваються дуже тактильно. Кейкапи Retro Beige виглядають ще краще, ніж на фото.",
    pros: ["Якість корпусу", "Звук", "Швидке виготовлення"],
    cons: ["Важка для поїздок"],
    photos: ["reviews/aurora-1.jpg", "reviews/aurora-2.jpg"],
    customization: { switches: "Holy Panda", keycaps: "Retro Beige", caseColor: "Silver" },
    verifiedPurchase: true,
    orderId: NumberInt(1),
    helpfulVotes: NumberInt(14),
    comments: [
      { author: "Andrii_M", text: "Які стабілізатори стоять?", createdAt: new Date("2026-09-13T08:10:00Z") },
      { author: "Олена К.", text: "Заводські, змащені — не тарахтять", createdAt: new Date("2026-09-13T09:02:00Z") },
    ],
    discussionId: D1,
    createdAt: new Date("2026-09-12T18:00:00Z"),
  },
  {
    _id: R.canvasOlena,
    productId: NumberInt(5),
    productName: "Килимок Canvas XL 900×400",
    productType: "Mousepad",
    author: { profileId: P.olena, displayName: "Олена К." },
    rating: NumberInt(4),
    text: "Принт яскравий, край прошитий акуратно. Перші дні був запах гуми.",
    customization: { print: "Власний принт", printUrl: "prints/olena-retro.png", size: "900×400" },
    verifiedPurchase: true,
    orderId: NumberInt(1),
    helpfulVotes: NumberInt(3),
    comments: [],
    createdAt: new Date("2026-09-14T10:30:00Z"),
  },
  {
    // покупка не через магазин -> verifiedPurchase: false, без orderId
    _id: R.pocketAndrii,
    productId: NumberInt(8),
    productName: "Клавіатура Pocket 60 Hot-Swap",
    productType: "Keyboard",
    author: { profileId: P.andrii, displayName: "Andrii_M" },
    rating: NumberInt(4),
    title: "Ідеальна для мінімалістичного столу",
    text: "Hot-swap працює без проблем, поставив Kailh Box White. Шкода, що немає бездротового режиму.",
    pros: ["Hot-swap", "Компактність"],
    cons: ["Лише дротова"],
    customization: { switches: "Kailh Box White", plate: "FR4", lubed: true },
    verifiedPurchase: false,
    helpfulVotes: NumberInt(6),
    createdAt: new Date("2026-09-22T20:15:00Z"),
  },
  {
    _id: R.deskbeatMaria,
    productId: NumberInt(4),
    productName: "Колонки DeskBeat 2.0",
    author: { profileId: P.maria, displayName: "Марія" },
    rating: NumberInt(3),
    text: "Звук нормальний, але басу замало.",
    createdAt: new Date("2026-10-06T11:00:00Z"),
  },
  {
    _id: R.auroraTaras,
    productId: NumberInt(1),
    productName: "Клавіатура Aurora 75 (алюміній)",
    productType: "Keyboard",
    author: { profileId: P.taras, displayName: "Тарас Б." },
    rating: NumberInt(4),
    text: "Дуже добра, але ціна з опціями виходить відчутна.",
    cons: ["Ціна з Holy Panda + Retro Beige"],
    verifiedPurchase: false,
    helpfulVotes: NumberInt(1),
    comments: [],
    createdAt: new Date("2026-10-02T16:45:00Z"),
  },
]);

seed("discussions", [
  {
    _id: D1,
    reviewId: R.auroraOlena,
    productId: NumberInt(1),
    title: "Aurora 75: Holy Panda чи Gateron Red?",
    isClosed: false,
    messageCount: NumberInt(3),
    lastMessageAt: new Date("2026-09-16T19:40:00Z"),
    messages: [
      {
        author: { profileId: P.taras, displayName: "Тарас Б." },
        text: "Для друку багато тексту що краще — Holy Panda чи лінійні?",
        createdAt: new Date("2026-09-15T17:00:00Z"),
      },
      {
        author: { profileId: P.olena, displayName: "Олена К." },
        text: "Мені тактильні зручніші: менше помилок при наборі.",
        replyTo: NumberInt(0),
        createdAt: new Date("2026-09-15T18:20:00Z"),
      },
      {
        author: { displayName: "Гість" },
        text: "Gateron Red тихіші, якщо працюєте в офісі.",
        createdAt: new Date("2026-09-16T19:40:00Z"),
      },
    ],
  },
]);

seed("builds", [
  {
    _id: ObjectId("66f0c2a1e4b0a1b2c3d43001"),
    title: "Retro beige desk",
    description: "Сетап у кольорах старих ПК",
    author: { profileId: P.olena, displayName: "Олена К." },
    items: [
      {
        productId: NumberInt(1), productName: "Клавіатура Aurora 75 (алюміній)", productType: "Keyboard",
        customization: { switches: "Holy Panda", keycaps: "Retro Beige", caseColor: "Silver" },
      },
      {
        productId: NumberInt(5), productName: "Килимок Canvas XL 900×400", productType: "Mousepad",
        customization: { size: "900×400", printUrl: "prints/olena-retro.png" },
      },
      {
        productId: NumberInt(7), productName: "Кабель Coil USB-C", productType: "Accessory",
        customization: { length: "1.5 м", connector: "GX16" },
      },
    ],
    tags: ["retro", "beige", "75%"],
    photos: ["builds/retro-1.jpg", "builds/retro-2.jpg", "builds/retro-3.jpg"],
    likes: NumberInt(42),
    createdAt: new Date("2026-09-18T12:00:00Z"),
  },
  {
    _id: ObjectId("66f0c2a1e4b0a1b2c3d43002"),
    title: "Minimal white 60%",
    author: { profileId: P.andrii, displayName: "Andrii_M" },
    items: [
      {
        productId: NumberInt(8), productName: "Клавіатура Pocket 60 Hot-Swap", productType: "Keyboard",
        customization: { switches: "Kailh Box White", plate: "FR4" },
      },
      {
        productId: NumberInt(2), productName: "Миша Glide Pro", productType: "Mouse",
        customization: { caseColor: "Lilac", coating: "Matte", dpi: NumberInt(1600) },
        note: "чекаю з виготовлення",
      },
    ],
    tags: ["minimal", "60%", "white"],
    likes: NumberInt(17),
    createdAt: new Date("2026-09-25T09:30:00Z"),
  },
  {
    _id: ObjectId("66f0c2a1e4b0a1b2c3d43003"),
    title: "Audio corner",
    author: { profileId: P.maria, displayName: "Марія" },
    items: [
      {
        productId: NumberInt(3), productName: "Навушники Studio Wave", productType: "Headphones",
        customization: { earpads: "Leather", engraving: "M.S." },
      },
      {
        productId: NumberInt(4), productName: "Колонки DeskBeat 2.0", productType: "Speakers",
        note: "на підставках під кутом 15°",
      },
    ],
    tags: ["audio"],
    likes: NumberInt(5),
    createdAt: new Date("2026-10-07T20:00:00Z"),
  },
]);

print("seed.js: done");
