﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace FlexibleDBMS
{

    public class SQLiteDBOperations: ISqlDbConnector
    {
        public delegate void Message(object sender, TextEventArgs e);
        public event Message EvntInfoMessage;

        string sqLiteConnectionString;
        ISQLConnectionSettings settings;

        public SQLiteDBOperations(ISQLConnectionSettings settings)
        {
            this.settings = settings;
            sqLiteConnectionString = $"Data Source = {settings.Database}; Version=3;";
        }

        public ISQLConnectionSettings GetSettings()
        {
            return settings;
        }

        private bool CheckUpDBStructure()
        {
            bool isGood = true;

            DbSchema schemaDB = null;
            string errors = string.Empty;

            try
            {
                schemaDB = DbSchema.LoadDB(settings.Database);

                foreach (var table in schemaDB.Tables)
                {
                    if (table.Value.Columns.Count == 0)
                    {
                        EvntInfoMessage?.Invoke(this, new TextEventArgs($"Ошибка в таблице: {table.Value.TableName} - отсутствуют колонки и структура данных в таблице."));
                    }
                }
            }
            catch (Exception e)
            {
                EvntInfoMessage?.Invoke(this, new TextEventArgs($"Ошибка в БД: {e.Message}:\r\n{e.ToString()}"));
                isGood = false;
            }
            finally
            {
                if (schemaDB?.Tables?.Count == 0)
                {
                    EvntInfoMessage?.Invoke(this, new TextEventArgs("Подключенная база данных пустая или же в ней отсутствуют какие-либо таблицы с данными!"));
                    EvntInfoMessage?.Invoke(this, new TextEventArgs("Предварительно создайте базу данных, таблицы и импортируйте/добавьте в них данные..."));
                    isGood = false;
                }
                schemaDB = null;
            }

            if (isGood)
            {
                EvntInfoMessage?.Invoke(this, new TextEventArgs($"В базе данных {settings.Database} со структурой все в порядке"));
            }

            return isGood;
        }

        public DataTable GetTable(string query)
        {
            DataTable dt = new DataTable();

            if (CheckUpDBStructure())
            {
                using (SqLiteDbWrapper readData = new SqLiteDbWrapper(sqLiteConnectionString, settings.Database))
                {
                    dt = readData.GetQueryResultAsTable(query);
                }
            }

            return dt;
        }

        /// <summary>
        /// get only simple query like 'SELECT DISTINCT name_column FROM name_table'
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        public IModelEntityDB<DBColumnModel> GetFilterList(IDictionary<string, string> columns, string table)
        {
            EvntInfoMessage?.Invoke(this, new TextEventArgs("В таблице: " + table + " " + columns?.Keys?.Count + " колонок "));

            IModelEntityDB<DBColumnModel> _table = new DBTableModel();
            IModelEntityDB<DBFilterModel> result;
            _table.Collection = new List<DBColumnModel>();
            if (CheckUpDBStructure())
            {
                foreach (var column in columns)
                {
                    //SQLiteDBOperations dBOperations
                    using (SqLiteDbWrapper readData = new SqLiteDbWrapper(sqLiteConnectionString, settings.Database))
                    {
                        result = readData.MakeFilterCollection(table, column.Key, column.Value);
                    }

                    EvntInfoMessage?.Invoke(this, new TextEventArgs($"Для фильтра отобрано {result.Collection.Count} строк"));

                    _table.Collection.Add((DBColumnModel)result);
                }
            }
            return _table;
        }

        public void TryMakeLocalDB()
        {
            string strQueryCreateObjectInDb = "CREATE TABLE IF NOT EXISTS 'CarAndOwner' ('Id' INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Plate TEXT, " +
                "Factory TEXT, Model TEXT, ManufactureYear TEXT, BodyNumber TEXT, ChassisNumber TEXT, EngineVolume TEXT, " +
                "Type TEXT, DRFO INTEGER, F TEXT, I TEXT, O TEXT, Birthday TEXT, " +
                "EDRPOU INTEGER, Name TEXT, City TEXT, District TEXT, Street TEXT, Building TEXT, BuildingBody TEXT, Apartment TEXT, " +
                "CodeOperation TEXT, CodeDate TEXT);";
            if (!File.Exists(settings.Database))
            {
                SQLiteConnection.CreateFile(settings.Database);
            }

            using (SqLiteDbWrapper dbWriter = new SqLiteDbWrapper(sqLiteConnectionString, settings.Database))
            {
                dbWriter.Execute("begin");
                dbWriter.Execute(strQueryCreateObjectInDb);
                dbWriter.Execute("end");

                if (CheckUpDBStructure())
                { EvntInfoMessage?.Invoke(this, new TextEventArgs("Таблицы в БД созданы")); }
                else
                {
                    EvntInfoMessage?.Invoke(this, new TextEventArgs("Ошибка создания таблиц в БД!"));
                }
            }
        }

        public void WriteListInLocalDB(IList<CarAndOwner> list)
        {
            string query =
                "INSERT OR REPLACE INTO 'CarAndOwner' (Plate, Factory, Model, ManufactureYear, BodyNumber, ChassisNumber, EngineVolume, Type, DRFO, F, I, O, Birthday, EDRPOU, " +
                "Name, City, District, Street, Building, BuildingBody, Apartment, CodeOperation, CodeDate) " +
                "VALUES (@Plate, @Factory, @Model, @ManufactureYear, @BodyNumber, @ChassisNumber, @EngineVolume, @Type, @DRFO, @F, @I, @O, @Birthday, @EDRPOU, " +
                "@Name, @City, @District, @Street, @Building, @BuildingBody, @Apartment, @CodeOperation, @CodeDate)";

            if (CheckUpDBStructure())
            {
                using (SqLiteDbWrapper dbWriter = new SqLiteDbWrapper(sqLiteConnectionString, settings.Database))
                {
                    EvntInfoMessage?.Invoke(this, new TextEventArgs($"Запись список в {list.Count} записей в базу список"));

                    dbWriter.Execute("begin");
                    foreach (var row in list)
                    {
                        using (SQLiteCommand SqlQuery = new SQLiteCommand(query, dbWriter.sqlConnection))
                        {
                            SqlQuery.Parameters.Add("@Plate", DbType.String).Value = row?.Plate;
                            SqlQuery.Parameters.Add("@Factory", DbType.String).Value = row?.Factory;
                            SqlQuery.Parameters.Add("@Model", DbType.String).Value = row?.Model;
                            SqlQuery.Parameters.Add("@ManufactureYear", DbType.String).Value = row?.ManufactureYear;
                            SqlQuery.Parameters.Add("@BodyNumber", DbType.String).Value = row?.BodyNumber;
                            SqlQuery.Parameters.Add("@ChassisNumber", DbType.String).Value = row?.ChassisNumber;
                            SqlQuery.Parameters.Add("@EngineVolume", DbType.String).Value = row?.EngineVolume;
                            SqlQuery.Parameters.Add("@Type", DbType.String).Value = row?.Type;
                            SqlQuery.Parameters.Add("@DRFO", DbType.Int32).Value = row?.DRFO;
                            SqlQuery.Parameters.Add("@F", DbType.String).Value = row?.F;
                            SqlQuery.Parameters.Add("@I", DbType.String).Value = row?.I;
                            SqlQuery.Parameters.Add("@O", DbType.String).Value = row?.O;
                            SqlQuery.Parameters.Add("@Birthday", DbType.String).Value = row?.Birthday;
                            SqlQuery.Parameters.Add("@EDRPOU", DbType.Int32).Value = row?.EDRPOU;
                            SqlQuery.Parameters.Add("@Name", DbType.String).Value = row?.Name;
                            SqlQuery.Parameters.Add("@City", DbType.String).Value = row?.City;
                            SqlQuery.Parameters.Add("@District", DbType.String).Value = row?.District;
                            SqlQuery.Parameters.Add("@Street", DbType.String).Value = row?.Street;
                            SqlQuery.Parameters.Add("@Building", DbType.String).Value = row?.Building;
                            SqlQuery.Parameters.Add("@BuildingBody", DbType.String).Value = row?.BuildingBody;
                            SqlQuery.Parameters.Add("@Apartment", DbType.String).Value = row?.Apartment;
                            SqlQuery.Parameters.Add("@CodeOperation", DbType.String).Value = row?.CodeOperation;
                            SqlQuery.Parameters.Add("@CodeDate", DbType.String).Value = row?.CodeDate;

                            dbWriter.ExecuteBulk(SqlQuery);
                        }
                    }

                    dbWriter.Execute("end");

                    EvntInfoMessage?.Invoke(this, new TextEventArgs("Запись списка завершена"));
                }
            }
            else
            {
                EvntInfoMessage?.Invoke(this, new TextEventArgs("Ошибка записи.\r\nПредварительно нужно настроить базу и подключение к базе!"));
            }
        }
    }
}