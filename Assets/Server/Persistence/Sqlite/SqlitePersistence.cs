using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using MuLike.Server.Persistence.Abstractions;
using Mono.Data.Sqlite;

namespace MuLike.Server.Persistence.Sqlite
{
    public sealed class SqliteServerDatabaseOptions
    {
        public string DatabasePath { get; set; }

        public string BuildConnectionString()
        {
            if (string.IsNullOrWhiteSpace(DatabasePath))
                throw new InvalidOperationException("DatabasePath is required.");

            return $"URI=file:{DatabasePath}";
        }
    }

    public sealed class SqliteConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(SqliteServerDatabaseOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _connectionString = options.BuildConnectionString();
        }

        public SqliteConnection CreateOpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }

    public sealed class SqliteMigrationRunner
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteMigrationRunner(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void EnsureMigrated()
        {
            using SqliteConnection connection = _connectionFactory.CreateOpenConnection();
            using SqliteTransaction tx = connection.BeginTransaction();

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS Account (
    AccountId      INTEGER PRIMARY KEY,
    Username       TEXT NOT NULL UNIQUE,
    AccountName    TEXT NOT NULL,
    PasswordHash   TEXT NOT NULL,
    IsActive       INTEGER NOT NULL,
    CreatedAtUtc   TEXT NOT NULL,
    UpdatedAtUtc   TEXT NOT NULL
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS Character (
    CharacterId    INTEGER PRIMARY KEY,
    AccountId      INTEGER NOT NULL,
    Name           TEXT NOT NULL,
    Level          INTEGER NOT NULL,
    MapId          INTEGER NOT NULL,
    PosX           REAL NOT NULL,
    PosY           REAL NOT NULL,
    PosZ           REAL NOT NULL,
    HpCurrent      INTEGER NOT NULL,
    HpMax          INTEGER NOT NULL,
    LastLoginUtc   TEXT NULL,
    LastLogoutUtc  TEXT NULL,
    FOREIGN KEY(AccountId) REFERENCES Account(AccountId)
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS InventoryItem (
    CharacterId    INTEGER NOT NULL,
    SlotIndex      INTEGER NOT NULL,
    ItemInstanceId INTEGER NOT NULL,
    ItemId         INTEGER NOT NULL,
    Quantity       INTEGER NOT NULL,
    EnhancementLevel INTEGER NOT NULL DEFAULT 0,
    ExcellentFlags INTEGER NOT NULL DEFAULT 0,
    SocketData     TEXT NOT NULL DEFAULT '',
    SellValue      INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(CharacterId, SlotIndex),
    FOREIGN KEY(CharacterId) REFERENCES Character(CharacterId)
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS EquipmentSlot (
    CharacterId    INTEGER NOT NULL,
    SlotName       TEXT NOT NULL,
    ItemInstanceId INTEGER NOT NULL,
    ItemId         INTEGER NOT NULL,
    EnhancementLevel INTEGER NOT NULL DEFAULT 0,
    ExcellentFlags INTEGER NOT NULL DEFAULT 0,
    SocketData     TEXT NOT NULL DEFAULT '',
    SellValue      INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(CharacterId, SlotName),
    FOREIGN KEY(CharacterId) REFERENCES Character(CharacterId)
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS SkillLoadout (
    CharacterId    INTEGER NOT NULL,
    SlotIndex      INTEGER NOT NULL,
    SkillId        INTEGER NOT NULL,
    PRIMARY KEY(CharacterId, SlotIndex),
    FOREIGN KEY(CharacterId) REFERENCES Character(CharacterId)
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS Pet (
    CharacterId    INTEGER PRIMARY KEY,
    PetId          INTEGER NOT NULL,
    Level          INTEGER NOT NULL,
    IsActive       INTEGER NOT NULL,
    FOREIGN KEY(CharacterId) REFERENCES Character(CharacterId)
);");

            ExecuteNonQuery(connection, tx, @"
CREATE TABLE IF NOT EXISTS MailCurrency (
    CharacterId    INTEGER PRIMARY KEY,
    Zen            INTEGER NOT NULL,
    Gems           INTEGER NOT NULL,
    Bless          INTEGER NOT NULL,
    Soul           INTEGER NOT NULL,
    FOREIGN KEY(CharacterId) REFERENCES Character(CharacterId)
);");

            tx.Commit();
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction tx, string sql)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public sealed class SqliteSeedRunner
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteSeedRunner(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void SeedIfEmpty(string adminPasswordHash, string testerPasswordHash)
        {
            using SqliteConnection connection = _connectionFactory.CreateOpenConnection();
            using SqliteTransaction tx = connection.BeginTransaction();

            long accountCount = ExecuteScalarInt64(connection, tx, "SELECT COUNT(1) FROM Account;");
            if (accountCount > 0)
            {
                tx.Commit();
                return;
            }

            DateTime now = DateTime.UtcNow;
            string nowText = ToUtcText(now);

            InsertAccount(connection, tx, 1, "admin", "admin", adminPasswordHash, true, nowText);
            InsertAccount(connection, tx, 2, "tester", "tester", testerPasswordHash, true, nowText);

            InsertCharacter(connection, tx, 1, 1, "admin", 1, 1, 0f, 0f, 0f, 100, 100, nowText, null);
            InsertCharacter(connection, tx, 2, 2, "tester", 1, 1, 1f, 0f, 1f, 100, 100, nowText, null);

            InsertInventory(connection, tx, 1, 0, 10001, 1001, 3);
            InsertInventory(connection, tx, 1, 1, 10002, 2001, 1);

            InsertEquipment(connection, tx, 1, "WeaponMain", 20001, 3001);
            InsertSkill(connection, tx, 1, 0, 1);
            InsertMailCurrency(connection, tx, 1, 10000, 10, 0, 0);

            tx.Commit();
        }

        private static long ExecuteScalarInt64(SqliteConnection connection, SqliteTransaction tx, string sql)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            object value = cmd.ExecuteScalar();
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static void InsertAccount(SqliteConnection c, SqliteTransaction tx, int accountId, string username, string accountName, string hash, bool isActive, string nowUtc)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO Account(AccountId, Username, AccountName, PasswordHash, IsActive, CreatedAtUtc, UpdatedAtUtc)
VALUES(@id,@u,@n,@h,@a,@c,@u2);";
            cmd.Parameters.AddWithValue("@id", accountId);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@n", accountName);
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@a", isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@c", nowUtc);
            cmd.Parameters.AddWithValue("@u2", nowUtc);
            cmd.ExecuteNonQuery();
        }

        private static void InsertCharacter(SqliteConnection c, SqliteTransaction tx, int characterId, int accountId, string name, int level, int mapId, float x, float y, float z, int hpCurrent, int hpMax, string lastLoginUtc, string lastLogoutUtc)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO Character(CharacterId, AccountId, Name, Level, MapId, PosX, PosY, PosZ, HpCurrent, HpMax, LastLoginUtc, LastLogoutUtc)
VALUES(@cid,@aid,@n,@lvl,@map,@x,@y,@z,@hp,@hpmax,@login,@logout);";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", accountId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@lvl", level);
            cmd.Parameters.AddWithValue("@map", mapId);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@z", z);
            cmd.Parameters.AddWithValue("@hp", hpCurrent);
            cmd.Parameters.AddWithValue("@hpmax", hpMax);
            cmd.Parameters.AddWithValue("@login", (object)lastLoginUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@logout", (object)lastLogoutUtc ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static void InsertInventory(SqliteConnection c, SqliteTransaction tx, int characterId, int slotIndex, long itemInstanceId, int itemId, int quantity)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO InventoryItem(CharacterId, SlotIndex, ItemInstanceId, ItemId, Quantity, EnhancementLevel, ExcellentFlags, SocketData, SellValue) VALUES(@c,@s,@x,@i,@q,@e,@f,@sd,@sv);";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", slotIndex);
            cmd.Parameters.AddWithValue("@x", itemInstanceId);
            cmd.Parameters.AddWithValue("@i", itemId);
            cmd.Parameters.AddWithValue("@q", quantity);
            cmd.Parameters.AddWithValue("@e", 0);
            cmd.Parameters.AddWithValue("@f", 0);
            cmd.Parameters.AddWithValue("@sd", "");
            cmd.Parameters.AddWithValue("@sv", 0);
            cmd.ExecuteNonQuery();
        }

        private static void InsertEquipment(SqliteConnection c, SqliteTransaction tx, int characterId, string slotName, long itemInstanceId, int itemId)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO EquipmentSlot(CharacterId, SlotName, ItemInstanceId, ItemId, EnhancementLevel, ExcellentFlags, SocketData, SellValue) VALUES(@c,@s,@x,@i,@e,@f,@sd,@sv);";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", slotName);
            cmd.Parameters.AddWithValue("@x", itemInstanceId);
            cmd.Parameters.AddWithValue("@i", itemId);
            cmd.Parameters.AddWithValue("@e", 0);
            cmd.Parameters.AddWithValue("@f", 0);
            cmd.Parameters.AddWithValue("@sd", "");
            cmd.Parameters.AddWithValue("@sv", 0);
            cmd.ExecuteNonQuery();
        }

        private static void InsertSkill(SqliteConnection c, SqliteTransaction tx, int characterId, int slotIndex, int skillId)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO SkillLoadout(CharacterId, SlotIndex, SkillId) VALUES(@c,@s,@k);";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", slotIndex);
            cmd.Parameters.AddWithValue("@k", skillId);
            cmd.ExecuteNonQuery();
        }

        private static void InsertMailCurrency(SqliteConnection c, SqliteTransaction tx, int characterId, long zen, int gems, int bless, int soul)
        {
            using SqliteCommand cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO MailCurrency(CharacterId, Zen, Gems, Bless, Soul) VALUES(@c,@z,@g,@b,@s);";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@z", zen);
            cmd.Parameters.AddWithValue("@g", gems);
            cmd.Parameters.AddWithValue("@b", bless);
            cmd.Parameters.AddWithValue("@s", soul);
            cmd.ExecuteNonQuery();
        }

        private static string ToUtcText(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }
    }

    public sealed class SqliteServerUnitOfWorkFactory : IServerUnitOfWorkFactory
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteServerUnitOfWorkFactory(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IServerUnitOfWork Create()
        {
            return new SqliteServerUnitOfWork(_connectionFactory.CreateOpenConnection());
        }
    }

    public sealed class SqliteServerUnitOfWork : IServerUnitOfWork
    {
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _tx;
        private bool _completed;

        public SqliteServerUnitOfWork(SqliteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _tx = _connection.BeginTransaction();

            Accounts = new SqliteAccountPersistenceRepository(_connection, _tx);
            Characters = new SqliteCharacterPersistenceRepository(_connection, _tx);
            InventoryItems = new SqliteInventoryItemPersistenceRepository(_connection, _tx);
            EquipmentSlots = new SqliteEquipmentSlotPersistenceRepository(_connection, _tx);
            SkillLoadouts = new SqliteSkillLoadoutPersistenceRepository(_connection, _tx);
            Pets = new SqlitePetPersistenceRepository(_connection, _tx);
            MailCurrencies = new SqliteMailCurrencyPersistenceRepository(_connection, _tx);
        }

        public IAccountPersistenceRepository Accounts { get; }
        public ICharacterPersistenceRepository Characters { get; }
        public IInventoryItemPersistenceRepository InventoryItems { get; }
        public IEquipmentSlotPersistenceRepository EquipmentSlots { get; }
        public ISkillLoadoutPersistenceRepository SkillLoadouts { get; }
        public IPetPersistenceRepository Pets { get; }
        public IMailCurrencyPersistenceRepository MailCurrencies { get; }

        public void Commit()
        {
            if (_completed) return;
            _tx.Commit();
            _completed = true;
        }

        public void Rollback()
        {
            if (_completed) return;
            _tx.Rollback();
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
                _tx.Rollback();

            _tx.Dispose();
            _connection.Dispose();
        }
    }

    internal abstract class SqliteRepositoryBase
    {
        protected readonly SqliteConnection Connection;
        protected readonly SqliteTransaction Transaction;

        protected SqliteRepositoryBase(SqliteConnection connection, SqliteTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        protected SqliteCommand Command(string sql)
        {
            SqliteCommand cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandText = sql;
            return cmd;
        }

        protected static string ToUtcText(DateTime value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        protected static DateTime? FromNullableUtcText(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
                return parsed.ToUniversalTime();

            return null;
        }
    }

    internal sealed class SqliteAccountPersistenceRepository : SqliteRepositoryBase, IAccountPersistenceRepository
    {
        public SqliteAccountPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public bool TryGetByUsername(string username, out AccountPersistenceModel account)
        {
            account = null;
            using SqliteCommand cmd = Command("SELECT AccountId, Username, AccountName, PasswordHash, IsActive, CreatedAtUtc, UpdatedAtUtc FROM Account WHERE Username=@u LIMIT 1;");
            cmd.Parameters.AddWithValue("@u", username);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            account = Read(reader);
            return true;
        }

        public bool TryGetByAccountId(int accountId, out AccountPersistenceModel account)
        {
            account = null;
            using SqliteCommand cmd = Command("SELECT AccountId, Username, AccountName, PasswordHash, IsActive, CreatedAtUtc, UpdatedAtUtc FROM Account WHERE AccountId=@id LIMIT 1;");
            cmd.Parameters.AddWithValue("@id", accountId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            account = Read(reader);
            return true;
        }

        public void Upsert(AccountPersistenceModel account)
        {
            using SqliteCommand cmd = Command(@"
INSERT INTO Account(AccountId, Username, AccountName, PasswordHash, IsActive, CreatedAtUtc, UpdatedAtUtc)
VALUES(@id,@u,@n,@h,@a,@c,@up)
ON CONFLICT(AccountId) DO UPDATE SET
Username=excluded.Username,
AccountName=excluded.AccountName,
PasswordHash=excluded.PasswordHash,
IsActive=excluded.IsActive,
UpdatedAtUtc=excluded.UpdatedAtUtc;");
            cmd.Parameters.AddWithValue("@id", account.AccountId);
            cmd.Parameters.AddWithValue("@u", account.Username);
            cmd.Parameters.AddWithValue("@n", account.AccountName);
            cmd.Parameters.AddWithValue("@h", account.PasswordHash);
            cmd.Parameters.AddWithValue("@a", account.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@c", ToUtcText(account.CreatedAtUtc));
            cmd.Parameters.AddWithValue("@up", ToUtcText(account.UpdatedAtUtc));
            cmd.ExecuteNonQuery();
        }

        private static AccountPersistenceModel Read(IDataRecord record)
        {
            return new AccountPersistenceModel
            {
                AccountId = record.GetInt32(0),
                Username = record.GetString(1),
                AccountName = record.GetString(2),
                PasswordHash = record.GetString(3),
                IsActive = record.GetInt32(4) == 1,
                CreatedAtUtc = DateTime.Parse(record.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
                UpdatedAtUtc = DateTime.Parse(record.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime()
            };
        }
    }

    internal sealed class SqliteCharacterPersistenceRepository : SqliteRepositoryBase, ICharacterPersistenceRepository
    {
        public SqliteCharacterPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public bool TryGetByAccountId(int accountId, out CharacterPersistenceModel character)
        {
            character = null;
            using SqliteCommand cmd = Command(@"SELECT CharacterId, AccountId, Name, Class, Level, MapId, PosX, PosY, PosZ, HpCurrent, HpMax, LastLoginUtc, LastLogoutUtc
FROM Character WHERE AccountId=@a LIMIT 1;");
            cmd.Parameters.AddWithValue("@a", accountId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            character = Read(reader);
            return true;
        }

        public IReadOnlyList<CharacterPersistenceModel> LoadByAccountId(int accountId)
        {
            var characters = new List<CharacterPersistenceModel>();
            using SqliteCommand cmd = Command(@"SELECT CharacterId, AccountId, Name, Class, Level, MapId, PosX, PosY, PosZ, HpCurrent, HpMax, LastLoginUtc, LastLogoutUtc
FROM Character WHERE AccountId=@a ORDER BY CharacterId;");
            cmd.Parameters.AddWithValue("@a", accountId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                characters.Add(Read(reader));
            }
            return characters;
        }

        public bool TryGetByCharacterId(int characterId, out CharacterPersistenceModel character)
        {
            character = null;
            using SqliteCommand cmd = Command(@"SELECT CharacterId, AccountId, Name, Class, Level, MapId, PosX, PosY, PosZ, HpCurrent, HpMax, LastLoginUtc, LastLogoutUtc
FROM Character WHERE CharacterId=@c LIMIT 1;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            character = Read(reader);
            return true;
        }

        public void Upsert(CharacterPersistenceModel character)
        {
            using SqliteCommand cmd = Command(@"
INSERT INTO Character(CharacterId, AccountId, Name, Class, Level, MapId, PosX, PosY, PosZ, HpCurrent, HpMax, LastLoginUtc, LastLogoutUtc)
VALUES(@cid,@aid,@n,@class,@lvl,@map,@x,@y,@z,@hp,@hpmax,@login,@logout)
ON CONFLICT(CharacterId) DO UPDATE SET
AccountId=excluded.AccountId,
Name=excluded.Name,
Class=excluded.Class,
Level=excluded.Level,
MapId=excluded.MapId,
PosX=excluded.PosX,
PosY=excluded.PosY,
PosZ=excluded.PosZ,
HpCurrent=excluded.HpCurrent,
HpMax=excluded.HpMax,
LastLoginUtc=excluded.LastLoginUtc,
LastLogoutUtc=excluded.LastLogoutUtc;");
            cmd.Parameters.AddWithValue("@cid", character.CharacterId);
            cmd.Parameters.AddWithValue("@aid", character.AccountId);
            cmd.Parameters.AddWithValue("@n", character.Name);
            cmd.Parameters.AddWithValue("@class", character.Class ?? "Warrior");
            cmd.Parameters.AddWithValue("@lvl", character.Level);
            cmd.Parameters.AddWithValue("@map", character.MapId);
            cmd.Parameters.AddWithValue("@x", character.PosX);
            cmd.Parameters.AddWithValue("@y", character.PosY);
            cmd.Parameters.AddWithValue("@z", character.PosZ);
            cmd.Parameters.AddWithValue("@hp", character.HpCurrent);
            cmd.Parameters.AddWithValue("@hpmax", character.HpMax);
            cmd.Parameters.AddWithValue("@login", character.LastLoginUtc.HasValue ? (object)ToUtcText(character.LastLoginUtc.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("@logout", character.LastLogoutUtc.HasValue ? (object)ToUtcText(character.LastLogoutUtc.Value) : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int characterId)
        {
            using SqliteCommand cmd = Command("DELETE FROM Character WHERE CharacterId=@c;");
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.ExecuteNonQuery();
        }

        public void MarkLogin(int characterId, DateTime loginUtc)
        {
            using SqliteCommand cmd = Command("UPDATE Character SET LastLoginUtc=@v WHERE CharacterId=@c;");
            cmd.Parameters.AddWithValue("@v", ToUtcText(loginUtc));
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.ExecuteNonQuery();
        }

        public void MarkLogout(int characterId, DateTime logoutUtc)
        {
            using SqliteCommand cmd = Command("UPDATE Character SET LastLogoutUtc=@v WHERE CharacterId=@c;");
            cmd.Parameters.AddWithValue("@v", ToUtcText(logoutUtc));
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.ExecuteNonQuery();
        }

        private static CharacterPersistenceModel Read(IDataRecord record)
        {
            return new CharacterPersistenceModel
            {
                CharacterId = record.GetInt32(0),
                AccountId = record.GetInt32(1),
                Name = record.GetString(2),
                Class = record.GetString(3),
                Level = record.GetInt32(4),
                MapId = record.GetInt32(5),
                PosX = Convert.ToSingle(record.GetDouble(6), CultureInfo.InvariantCulture),
                PosY = Convert.ToSingle(record.GetDouble(7), CultureInfo.InvariantCulture),
                PosZ = Convert.ToSingle(record.GetDouble(8), CultureInfo.InvariantCulture),
                HpCurrent = record.GetInt32(9),
                HpMax = record.GetInt32(10),
                LastLoginUtc = FromNullableUtcText(record[11]),
                LastLogoutUtc = FromNullableUtcText(record[12])
            };
        }
    }

    internal sealed class SqliteInventoryItemPersistenceRepository : SqliteRepositoryBase, IInventoryItemPersistenceRepository
    {
        public SqliteInventoryItemPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public IReadOnlyList<InventoryItemPersistenceModel> LoadByCharacterId(int characterId)
        {
            var list = new List<InventoryItemPersistenceModel>();
            using SqliteCommand cmd = Command("SELECT CharacterId, SlotIndex, ItemInstanceId, ItemId, Quantity, EnhancementLevel, ExcellentFlags, SocketData, SellValue FROM InventoryItem WHERE CharacterId=@c ORDER BY SlotIndex;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new InventoryItemPersistenceModel
                {
                    CharacterId = reader.GetInt32(0),
                    SlotIndex = reader.GetInt32(1),
                    ItemInstanceId = reader.GetInt64(2),
                    ItemId = reader.GetInt32(3),
                    Quantity = reader.GetInt32(4),
                    EnhancementLevel = reader.GetInt32(5),
                    ExcellentFlags = reader.GetInt32(6),
                    SocketData = reader.GetString(7),
                    SellValue = reader.GetInt32(8)
                });
            }

            return list;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<InventoryItemPersistenceModel> items)
        {
            using (SqliteCommand delete = Command("DELETE FROM InventoryItem WHERE CharacterId=@c;"))
            {
                delete.Parameters.AddWithValue("@c", characterId);
                delete.ExecuteNonQuery();
            }

            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemPersistenceModel item = items[i];
                using SqliteCommand insert = Command("INSERT INTO InventoryItem(CharacterId, SlotIndex, ItemInstanceId, ItemId, Quantity, EnhancementLevel, ExcellentFlags, SocketData, SellValue) VALUES(@c,@s,@x,@i,@q,@e,@f,@sd,@sv);");
                insert.Parameters.AddWithValue("@c", characterId);
                insert.Parameters.AddWithValue("@s", item.SlotIndex);
                insert.Parameters.AddWithValue("@x", item.ItemInstanceId);
                insert.Parameters.AddWithValue("@i", item.ItemId);
                insert.Parameters.AddWithValue("@q", item.Quantity);
                insert.Parameters.AddWithValue("@e", item.EnhancementLevel);
                insert.Parameters.AddWithValue("@f", item.ExcellentFlags);
                insert.Parameters.AddWithValue("@sd", item.SocketData ?? string.Empty);
                insert.Parameters.AddWithValue("@sv", item.SellValue);
                insert.ExecuteNonQuery();
            }
        }
    }

    internal sealed class SqliteEquipmentSlotPersistenceRepository : SqliteRepositoryBase, IEquipmentSlotPersistenceRepository
    {
        public SqliteEquipmentSlotPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public IReadOnlyList<EquipmentSlotPersistenceModel> LoadByCharacterId(int characterId)
        {
            var list = new List<EquipmentSlotPersistenceModel>();
            using SqliteCommand cmd = Command("SELECT CharacterId, SlotName, ItemInstanceId, ItemId, EnhancementLevel, ExcellentFlags, SocketData, SellValue FROM EquipmentSlot WHERE CharacterId=@c ORDER BY SlotName;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EquipmentSlotPersistenceModel
                {
                    CharacterId = reader.GetInt32(0),
                    SlotName = reader.GetString(1),
                    ItemInstanceId = reader.GetInt64(2),
                    ItemId = reader.GetInt32(3),
                    EnhancementLevel = reader.GetInt32(4),
                    ExcellentFlags = reader.GetInt32(5),
                    SocketData = reader.GetString(6),
                    SellValue = reader.GetInt32(7)
                });
            }

            return list;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<EquipmentSlotPersistenceModel> slots)
        {
            using (SqliteCommand delete = Command("DELETE FROM EquipmentSlot WHERE CharacterId=@c;"))
            {
                delete.Parameters.AddWithValue("@c", characterId);
                delete.ExecuteNonQuery();
            }

            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotPersistenceModel slot = slots[i];
                using SqliteCommand insert = Command("INSERT INTO EquipmentSlot(CharacterId, SlotName, ItemInstanceId, ItemId, EnhancementLevel, ExcellentFlags, SocketData, SellValue) VALUES(@c,@s,@x,@i,@e,@f,@sd,@sv);");
                insert.Parameters.AddWithValue("@c", characterId);
                insert.Parameters.AddWithValue("@s", slot.SlotName);
                insert.Parameters.AddWithValue("@x", slot.ItemInstanceId);
                insert.Parameters.AddWithValue("@i", slot.ItemId);
                insert.Parameters.AddWithValue("@e", slot.EnhancementLevel);
                insert.Parameters.AddWithValue("@f", slot.ExcellentFlags);
                insert.Parameters.AddWithValue("@sd", slot.SocketData ?? string.Empty);
                insert.Parameters.AddWithValue("@sv", slot.SellValue);
                insert.ExecuteNonQuery();
            }
        }
    }

    internal sealed class SqliteSkillLoadoutPersistenceRepository : SqliteRepositoryBase, ISkillLoadoutPersistenceRepository
    {
        public SqliteSkillLoadoutPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public IReadOnlyList<SkillLoadoutPersistenceModel> LoadByCharacterId(int characterId)
        {
            var list = new List<SkillLoadoutPersistenceModel>();
            using SqliteCommand cmd = Command("SELECT CharacterId, SlotIndex, SkillId FROM SkillLoadout WHERE CharacterId=@c ORDER BY SlotIndex;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SkillLoadoutPersistenceModel
                {
                    CharacterId = reader.GetInt32(0),
                    SlotIndex = reader.GetInt32(1),
                    SkillId = reader.GetInt32(2)
                });
            }

            return list;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<SkillLoadoutPersistenceModel> skills)
        {
            using (SqliteCommand delete = Command("DELETE FROM SkillLoadout WHERE CharacterId=@c;"))
            {
                delete.Parameters.AddWithValue("@c", characterId);
                delete.ExecuteNonQuery();
            }

            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                SkillLoadoutPersistenceModel skill = skills[i];
                using SqliteCommand insert = Command("INSERT INTO SkillLoadout(CharacterId, SlotIndex, SkillId) VALUES(@c,@s,@k);");
                insert.Parameters.AddWithValue("@c", characterId);
                insert.Parameters.AddWithValue("@s", skill.SlotIndex);
                insert.Parameters.AddWithValue("@k", skill.SkillId);
                insert.ExecuteNonQuery();
            }
        }
    }

    internal sealed class SqlitePetPersistenceRepository : SqliteRepositoryBase, IPetPersistenceRepository
    {
        public SqlitePetPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public bool TryGetByCharacterId(int characterId, out PetPersistenceModel pet)
        {
            pet = null;
            using SqliteCommand cmd = Command("SELECT CharacterId, PetId, Level, IsActive FROM Pet WHERE CharacterId=@c LIMIT 1;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            pet = new PetPersistenceModel
            {
                CharacterId = reader.GetInt32(0),
                PetId = reader.GetInt32(1),
                Level = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) == 1
            };
            return true;
        }

        public void Upsert(PetPersistenceModel pet)
        {
            using SqliteCommand cmd = Command(@"
INSERT INTO Pet(CharacterId, PetId, Level, IsActive)
VALUES(@c,@p,@l,@a)
ON CONFLICT(CharacterId) DO UPDATE SET
PetId=excluded.PetId,
Level=excluded.Level,
IsActive=excluded.IsActive;");
            cmd.Parameters.AddWithValue("@c", pet.CharacterId);
            cmd.Parameters.AddWithValue("@p", pet.PetId);
            cmd.Parameters.AddWithValue("@l", pet.Level);
            cmd.Parameters.AddWithValue("@a", pet.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    internal sealed class SqliteMailCurrencyPersistenceRepository : SqliteRepositoryBase, IMailCurrencyPersistenceRepository
    {
        public SqliteMailCurrencyPersistenceRepository(SqliteConnection connection, SqliteTransaction transaction) : base(connection, transaction) { }

        public bool TryGetByCharacterId(int characterId, out MailCurrencyPersistenceModel wallet)
        {
            wallet = null;
            using SqliteCommand cmd = Command("SELECT CharacterId, Zen, Gems, Bless, Soul FROM MailCurrency WHERE CharacterId=@c LIMIT 1;");
            cmd.Parameters.AddWithValue("@c", characterId);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            wallet = new MailCurrencyPersistenceModel
            {
                CharacterId = reader.GetInt32(0),
                Zen = reader.GetInt64(1),
                Gems = reader.GetInt32(2),
                Bless = reader.GetInt32(3),
                Soul = reader.GetInt32(4)
            };
            return true;
        }

        public void Upsert(MailCurrencyPersistenceModel wallet)
        {
            using SqliteCommand cmd = Command(@"
INSERT INTO MailCurrency(CharacterId, Zen, Gems, Bless, Soul)
VALUES(@c,@z,@g,@b,@s)
ON CONFLICT(CharacterId) DO UPDATE SET
Zen=excluded.Zen,
Gems=excluded.Gems,
Bless=excluded.Bless,
Soul=excluded.Soul;");
            cmd.Parameters.AddWithValue("@c", wallet.CharacterId);
            cmd.Parameters.AddWithValue("@z", wallet.Zen);
            cmd.Parameters.AddWithValue("@g", wallet.Gems);
            cmd.Parameters.AddWithValue("@b", wallet.Bless);
            cmd.Parameters.AddWithValue("@s", wallet.Soul);
            cmd.ExecuteNonQuery();
        }
    }
}
