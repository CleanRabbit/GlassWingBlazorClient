// Seeds fixture data several E2E tests need beyond what DevPlayerSeeder + the one auto-created
// starter rat provide: fixed-name rats (RatDetailTests: an opposite-sex breeding pair, and a rat
// with an undiagnosed illness no in-app action reliably induces on demand) and enough currency to
// actually buy something from the shop (ShopTests — a fresh DevPlayer starts at 0). Run once per
// test run, after the Testing API has started (so the DevPlayer already exists) and before the
// suite itself — see run-e2e-tests.ps1. All other Rat fields are intentionally omitted and fall
// back to RatDocument's own property initializers (Adult, Healthy, not pregnant/retired, etc.)
// exactly the way a normal starter rat would.

const ownerId = "000000000000000000000001"; // DevPlayerSeeder.DevPlayerId
// Pulled live from a freshly-generated starter rat rather than hand-built — a stale hand-built
// string silently passed compilation but crashed at read time with "Coat section must be 54
// chars, got 38" the moment any endpoint (e.g. GET /api/tricks) actually decoded it: the genome
// has grown (Task 19's Cognition loci, Task 27's Coat loci) since that string was authored, and
// nothing statically checks a raw literal against the current section-length constants.
const dna = "aAbbCCDDEEhhpPRrSsmMUugGiiwwwwWwLlOOqqffkkkKkKnNttxxYY|ZzZzTtTtLlLlVvVvVvKkKkXxXxXX|FfCcBbNnMmPpGgJjWw|MmAaDdFfSsIiEeVvRr|MmNnIiLlTtCcPpVvAaDd|IiIiIiRrNnTtGg";
const dateOfBirth = new Date("2025-01-01T00:00:00Z");

// _id is stored as a real ObjectId (BsonRepresentation on a string Id property) — a plain string
// filter wouldn't match it.
db.players.updateOne({ _id: ObjectId(ownerId) }, { $set: { Currency: 10000 } });

db.rats.insertMany([
  {
    OwnerId: ownerId,
    Name: "Dandelion",
    GeneticDna: dna,
    DateOfBirth: dateOfBirth,
    CreatedAt: dateOfBirth,
    Sex: "Female"
  },
  {
    OwnerId: ownerId,
    Name: "Robin",
    GeneticDna: dna,
    DateOfBirth: dateOfBirth,
    CreatedAt: dateOfBirth,
    Sex: "Male"
  },
  {
    OwnerId: ownerId,
    Name: "Cider",
    GeneticDna: dna,
    DateOfBirth: dateOfBirth,
    CreatedAt: dateOfBirth,
    Sex: "Female",
    HealthState: {
      Vitality: "Healthy",
      BodyLengthCm: 20,
      ActiveIllnesses: [
        { IllnessId: "uri", StartedAt: new Date() }
      ]
    }
  }
]);
