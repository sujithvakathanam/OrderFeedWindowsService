using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Threading;
using Asos.Fulfilment.Delivery.Infrastructure.Provider;
using Dapper;

namespace IntegrationTests
{
    public class Datahelper
    {
        private readonly string _connectionStringBackoffice;
        private readonly string _connectionStringStockAllocation;
        private readonly IDateTimeProvider _dateTimeProvider;

        public Datahelper(IDateTimeProvider dateTimeProvider)
        {
            _connectionStringBackoffice = ConfigurationManager.ConnectionStrings["BackofficeDb"].ConnectionString;
            _connectionStringStockAllocation = ConfigurationManager.ConnectionStrings["AsosStockAllocationConnectionString"].ConnectionString;
            _dateTimeProvider = dateTimeProvider;
        }

        public IEnumerable<dynamic> GetOrderFromQueue(int[] receiptIds)
        {
            var receiptId = BuildValuesForSqlStatement(receiptIds);

            var sql = string.Format(@"select w.ReceiptId as ReceiptId,w.ReceiptItemId as ReceiptItemId, w.WarehouseId as WarehouseId,r.StatusId as StatusId
                        From WmsDespatchAdviceQueue w
                        JOIN Receipt r ON r.receiptId = w.receiptId
                        where r.receiptId in ({0})", receiptId);
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return connection.Query<dynamic>(sql).ToList();
            }

        }
      


        public void InsertIntoWmsDespatchAdviceQueue(int orderId, int orderItemId, int warehouseId)
        {

            var insertRecordIntoDespatchAdviceQueue =
                "Insert Into WmsDespatchAdviceQueue values(@ReceiptId,@ReceiptItemId,@ActionType," +
                "@ProcessPriority,@DataEntered,@DateSent,@BatchName," +
                "@WarehouseId) ";


            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(insertRecordIntoDespatchAdviceQueue, new
                {
                    @ReceiptId = orderId,
                    @ReceiptItemId = orderItemId,
                    @ActionType = 'I',
                    @ProcessPriority = 1,
                    @DataEntered = _dateTimeProvider.Now,
                    @DateSent = (string)null,
                    @BatchName = String.Empty,
                    @WarehouseId = warehouseId

                });
            }
        }


        public void InsertIntoPOSItemForMessagePump(int orderId, int orderItemId)
        {
            const string insertRecordIntoPOSItem =
                "delete PW from POSItemWarehouse PW join POSItem P on PW.POSItemId = P.POSItemId where P.ReceiptItemId = @ReceiptItemId; " +
                "delete from POSItem where ReceiptId = @ReceiptId and ReceiptItemId = @ReceiptItemId; " +
                "Insert Into POSItem (ReceiptId,ReceiptItemId,ReceiptPaymentId,Quantity,PriceIncTax,Tax,CurrencyId,DeliverToCountryId,ExchangeRate,IsChequeRefund,IsStockReturn,DateEntered,DateModified) " +
                "values (@ReceiptId,@ReceiptItemId,@ReceiptPaymentId,@Quantity,@PriceIncTax,@Tax,@CurrencyId,@DeliverToCountryId,@ExchangeRate,@IsChequeRefund,@IsStockReturn,@DateEntered,@DateModified);";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(insertRecordIntoPOSItem, new
                {
                    @ReceiptId = orderId,
                    @ReceiptItemId = orderItemId,
                    @ReceiptPaymentId = -1,
                    @Quantity = 1,
                    @PriceIncTax = 1,
                    @Tax = 1,
                    @CurrencyId = -1,
                    @DeliverToCountryId = -1,
                    @ExchangeRate = 1,
                    @IsChequeRefund = 0,
                    @IsStockReturn = 0,
                    @DateEntered = _dateTimeProvider.Now,
                    @DateModified = _dateTimeProvider.Now
                });
            }
        }


        public void InsertIntoReceiptItemForMessagePump(int orderId, int orderItemId)
        {
            const string insertRecordReceiptItem =
                "delete from ReceiptItem where ReceiptItemId = @ReceiptItemId; " +
                "Set Identity_insert ReceiptItem ON; " +
                "Insert Into ReceiptItem (ReceiptItemId,ReceiptId,ReceiptDropId,InventoryId,SKU,StatusId,TaxSetupID,ShortDescription,Tax,PriceIncTax,DiscountExempt,Quantity,DateEntered,DateModified) " +
                "values (@ReceiptItemId,@ReceiptId,@ReceiptDropId,@InventoryId,@SKU,@StatusId,@TaxSetupID,@ShortDescription,@Tax,@PriceIncTax,@DiscountExempt,@Quantity,@DateEntered,@DateModified); " +
                "Set Identity_insert ReceiptItem OFF;";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(insertRecordReceiptItem, new
                {
                    @ReceiptItemId = orderItemId,
                    @ReceiptId = orderId,
                    @ReceiptDropId = -1,
                    @InventoryId = -1,
                    @SKU = "MPSKU",
                    @StatusId = -1,
                    @TaxSetupID = -1,
                    @ShortDescription = string.Empty,
                    @Tax = 1,
                    @PriceIncTax = 1,
                    @DiscountExempt = 0,
                    @Quantity = 1,
                    @DateEntered = DateTime.Now,
                    @DateModified = DateTime.Now
                });
            }
        }


        public static IEnumerable<dynamic> GetAndDeleteSubscriptionsForPublisher()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["NServiceBus/Persistence"].ConnectionString;
            using (var connection = new SqlConnection(connectionString))
            {
                var subscriptions = connection.Query("select * from [Subscription] ").ToList();
                connection.Execute("delete from [Subscription]");
                return subscriptions;
            }
        }

        public static void DeleteTimeoutEntity(string servicePrefix)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["NServiceBus/Persistence"].ConnectionString;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Execute("delete from TimeoutEntity where Destination like @Destination + '%'", new { @Destination = servicePrefix });
            }
        }

        public static IEnumerable<dynamic> SelectTimeoutEntity(string servicePrefix)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["NServiceBus/Persistence"].ConnectionString;
            using (var connection = new SqlConnection(connectionString))
            {
                return connection.Query("select * from TimeoutEntity where Destination like @Destination + '%'", new { @Destination = servicePrefix });
            }
        }

        public void InsertIntoReceipt(int orderId, int customerId, int statusId, int shippingSubscriptionId)
        {
            var insertIntoReceipt =
                "delete from receipt where receiptId = @receiptId;  " +                                 
                "Set Identity_Insert Receipt ON;  " +
                "Insert Into Receipt(ReceiptId,DateEntered,DateModified,OldReceiptId,ReceiptTypeId,ReceiptPaymentId,  " + 
                "CustomerId,StatusId,CurrencyId,ExchangeRate,IPAddress,AffiliateId,CampaignId,BaseReceiptId,ShippingSubscriptionId)  " +
                "values(@ReceiptId,@DateEntered,@DateModified,@OldReceiptId,@ReceiptTypeId,@ReceiptPaymentId,  " +
                "@CustomerId,@StatusId,@CurrencyId,@ExchangeRate,@IPAddress,@AffiliateId,@CampaignId,@BaseReceiptId,@ShippingSubscriptionId)  " +
                "Set Identity_Insert Receipt OFF;";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(insertIntoReceipt, new
                {
                    @ReceiptId = orderId,
                    @DateEntered = _dateTimeProvider.Now,
                    @DateModified = _dateTimeProvider.Now,
                    @OldReceiptId = orderId,
                    @ReceiptTypeId = 0,
                    @ReceiptPaymentId = 1,
                    @CustomerId = customerId,
                    @StatusId = statusId,
                    @CurrencyId = 1,
                    @ExchangeRate = 1.00000,
                    @IPAddress = "10.177.61.250", // arbitrary IP address for testing
                    @AffiliateId = 0,
                    @CampaignId = 0,
                    @BaseReceiptId = orderId,
                    @ShippingSubscriptionId = shippingSubscriptionId
                });
            }
            InsertIntoReceiptSiteId(orderId);
        }

        public void UpdateCustomerId(int orderId, int customerId)
        {
            var insertIntoReceipt = "update Receipt set CustomerId = @CustomerId where receiptId = @ReceiptId;";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(insertIntoReceipt, new
                {
                    @ReceiptId = orderId,
                    @CustomerId = customerId,
                });
            }
        }

        public void InsertIntoReceiptDrop(int receiptId, int? receiptDropId, string countryName)
        {
            var sql = "delete from receiptDrop where receiptDropId = @ReceiptDropId; " +
                      "Set Identity_insert ReceiptDrop ON " +
                      "insert into receiptdrop(ReceiptDropId,DateEntered,DateModified,ReceiptId,FirstName,LastName," +
                      "TelephoneDaytime,TelephoneMobile,Address1,Address2,City,County,PostCode,CountryId,ShippingMethodId,ShippingAmount,TaxSetUpId,ShippingTypeId,AddressIsDefault)" +
                      "values(@ReceiptDropId,@DateEntered,@DateModified,@ReceiptId,@FirstName,@LastName,@TelephoneDaytime,@TelephoneMobile,@Address1,@Address2," +
                      "@City,@County,@PostCode,@CountryId,@ShippingMethodId,@ShippingAmount,@TaxSetUpId,@ShippingTypeId,@AddressIsDefault)" +
                      "Set Identity_insert ReceiptDrop Off;";
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                var countryId = connection.Query<int>("select countryId from country where CountryName = @countryName",
                     new { countryName }).FirstOrDefault();
                if (countryId == 0)
                    throw new SystemException(@"Valid Country Name not entered in the feature file in the Given Statement 
                                       'an order that needs delivering to the VALIDCOUNTRYNAME has been lined for the fulfilment service to pick up'
                                       Make sure the country Name is as per the name entered
                                          in the 'Country' Table in the Backoffice Database");
                connection.Execute(sql,
                    new
                    {
                        @ReceiptDropId = receiptDropId,
                        @DateEntered = _dateTimeProvider.Now,
                        @DateModified = _dateTimeProvider.Now,
                        @ReceiptId = receiptId,
                        @FirstName = "Suniventen",
                        @LastName = "Team",
                        @TelephoneDaytime = "1234567899",
                        @TelephoneMobile = "9987654321",
                        @Address1 = "1 Kingsway",
                        @Address2 = "Camden Town",
                        @City = "London",
                        @County = "Greater London",
                        @PostCode = "NW1 7FB",
                        @CountryId = countryId,
                        @ShippingMethodId = 1,
                        @ShippingAmount = 0,
                        @TaxSetUpId = 1,
                        @ShippingTypeId = 1,
                        @AddressIsDefault = 1
                    });
            }
        }

        public void InsertIntoReceiptItem(int receiptId, int receiptItemId, int? receiptDropId, string sKU)
        {
            var inventoryDetails = GetInventoryDetailsForSku(sKU);
            var sql = "delete from ReceiptItemMarkdown where receiptItemId = @ReceiptItemId; " +
                      "delete from receiptItem where receiptItemId = @ReceiptItemId; " +
                      "Set Identity_insert ReceiptItem ON " +
                      "INSERT INTO ReceiptItem(ReceiptItemId,DateEntered,DateModified,ReceiptId,ReceiptDropId,InventoryId,StatusId,SKU, " +
                      "ShortDescription,Quantity,Tax,PriceIncTax,DiscountExempt,PercentDiscountAvailable,ReceiptItemTracking,TaxSetupID) " +
                      "VALUES(@ReceiptItemId,@DateEntered,@DateModified,@ReceiptId,@ReceiptDropId,@InventoryId,@StatusId,@SKU,@ShortDescription," +
                      "@Quantity,@Tax,@PriceIncTax,@DiscountExempt,@PercentDiscountAvailable,@ReceiptItemTracking,@TaxSetupID)" +
                      "Set Identity_insert ReceiptItem OFF";
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                string Null = null;
                connection.Open();
                connection.Execute(sql, new
                {
                    @ReceiptItemId = receiptItemId,
                    @DateEntered = _dateTimeProvider.Now,
                    @DateModified = _dateTimeProvider.Now,
                    @ReceiptId = receiptId,
                    @ReceiptDropId = receiptDropId,
                    @InventoryId = (int)inventoryDetails.InventoryId,
                    @StatusId = 0,
                    @SKU = sKU,
                    @ShortDescription = "Good Stuff",
                    @Quantity = 1,
                    @Tax = 1,
                    @PriceIncTax = (decimal)inventoryDetails.AverageUnitCost,
                    @DiscountExempt = 0,
                    @PercentDiscountAvailable = 0,
                    @ReceiptItemTracking = Null,
                    @TaxSetupID = 1
                });
                InsertIntoReceiptMarkDownReference(receiptItemId);
            }
        }

        private void InsertIntoReceiptMarkDownReference(int receiptItemId)
        {
            var sql = "delete from ReceiptItemMarkdown where receiptItemId = @ReceiptItemId; " +
                     "insert into ReceiptItemMarkdown (ReceiptItemId,MarkdownReference) values (@ReceiptItemId,@MarkdownReference);";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                connection.Execute(sql,
                    new
                    {
                        @ReceiptItemId = receiptItemId,
                        @MarkdownReference = 1
                    });
            }

        }

        public void InsertIntoPositemTableForSales(int? posItemId, int receiptId, int? receiptItemId, int? receiptDropId, int? receiptDiscountId, int despatchConfirmationId)
        {
            var receiptItemDetails = GetReceiptItemDetailsForReceiptItem(receiptItemId);
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                int i = 1;
                if (receiptDiscountId != null)
                    i = -1;
                string Null = null;
                var sql = "delete PosItemWarehouse from PositemWarehouse pow " +
                         "join positem pos ON pos.positemId = pow.positemId " +
                         "where pos.positemId = @positemId " +
                         "delete from positem where positemId = @positemId " +
                    "Set Identity_insert Positem ON INSERT INTO Positem(POSItemId,ReceiptId,ReceiptPaymentId,ReceiptDropId,ReceiptItemId,ReceiptDiscountId,VoidItemId" +
                    ",BaseReceiptId,Quantity,PriceIncTax,Tax,CurrencyId,DeliverToCountryId,DateEntered,AccFileIdCash,AccFileDateCash" +
                    ",PaymentLedgerId,ExchangeRate,AccFileIdSales,AccFileDateSales,CostPriceIncTax,CostPriceTax,CostPriceExchangeRate" +
                    ",InventoryId,VoidHeaderId,POSTransactionGuid,DateBilled,DateRefunded,DateCancelled,DateShipped,DateReturned,IsChequeRefund" +
                    ",IsStockReturn,CashDirection,IsEu,DespatchConfirmationId,ReceiptVoucherId,AccFileIdFraud,AccFileDateFraud,DateModified" +
                    ",ReceiptShippingSubscriptionId)     " +
                    "VALUES (@POSItemId,@ReceiptId,@ReceiptPaymentId,@ReceiptDropId,@ReceiptItemId,@ReceiptDiscountId,@VoidItemId,@BaseReceiptId," +
                    "@Quantity,@PriceIncTax,@Tax,@CurrencyId,@DeliverToCountryId,@DateEntered,@AccFileIdCash,@AccFileDateCash,@PaymentLedgerId," +
                    "@ExchangeRate,@AccFileIdSales,@AccFileDateSales,@CostPriceIncTax,@CostPriceTax,@CostPriceExchangeRate,@InventoryId," +
                    "@VoidHeaderId,@POSTransactionGuid,@DateBilled,@DateRefunded,@DateCancelled,@DateShipped,@DateReturned,@IsChequeRefund," +
                    "@IsStockReturn,@CashDirection,@IsEu,@DespatchConfirmationId,@ReceiptVoucherId,@AccFileIdFraud,@AccFileDateFraud," +
                    "@DateModified,@ReceiptShippingSubscriptionId); Set Identity_insert Positem OFF";
                connection.Open();

                connection.Execute(sql,
                    new
                    {
                        @POSItemId = posItemId,
                        @ReceiptId = receiptId,
                        @ReceiptPaymentId = 1,
                        @ReceiptDropId = receiptDropId,
                        @ReceiptItemId = receiptItemId,
                        @ReceiptDiscountId = receiptDiscountId,
                        @VoidItemId = Null,
                        @BaseReceiptId = receiptId,
                        @Quantity = 1,
                        @PriceIncTax = (decimal)receiptItemDetails.PriceIncTax * i,
                        @Tax = (decimal)receiptItemDetails.Tax * i,
                        @CurrencyId = 1,
                        @DeliverToCountryId = 2,
                        @DateEntered = _dateTimeProvider.Now.AddDays(-2),
                        @AccFileIdCash = Null,
                        @AccFileDateCash = Null,
                        @PaymentLedgerId = 12,
                        @ExchangeRate = 1,
                        @AccFileIdSales = Null,
                        @AccFileDateSales = Null,
                        @CostPriceIncTax = (decimal)receiptItemDetails.PriceIncTax / 5,
                        @CostPriceTax = (decimal)receiptItemDetails.PriceIncTax / 8,
                        @CostPriceExchangeRate = 0,
                        @InventoryId = 123,
                        @VoidHeaderId = Null,
                        @POSTransactionGuid = "ABC123",
                        @DateBilled = _dateTimeProvider.Now.AddDays(-1),
                        @DateRefunded = Null,
                        @DateCancelled = Null,
                        @DateShipped = _dateTimeProvider.Now.AddDays(-1),
                        @DateReturned = Null,
                        @IsChequeRefund = (byte)0,
                        @IsStockReturn = (byte)0,
                        @CashDirection = 'I',
                        @IsEu = 0,
                        @DespatchConfirmationId = despatchConfirmationId,
                        @ReceiptVoucherId = Null,
                        @AccFileIdFraud = Null,
                        @AccFileDateFraud = Null,
                        @DateModified = _dateTimeProvider.Now.AddDays(-1),
                        @ReceiptShippingSubscriptionId = Null
                    });

            }
        }

        public void InsertIntoReceiptDiscountTable(int receiptDiscountId, int receiptId, decimal discountTotal, decimal percentageDiscount)
        {
            decimal disAmount = 0;
            if (percentageDiscount == 0)
                disAmount = discountTotal;

            var sql = "delete from ReceiptDiscount where ReceiptDiscountId = @receiptDiscountId " +
                      "Set Identity_insert ReceiptDiscount ON " +
                      "Insert into ReceiptDiscount(ReceiptDiscountId,ReceiptId,DateEntered,DiscountCodeId,TypeId,DiscountAmount,DiscountPercentage," +
                      "MinSpend,MaxSpend,ValidFrom,ValidTo,TotalCustomerCount,AppliesToSaleItems,SingleUse,DiscountTotal,DateModified) " +
                      "values(@ReceiptDiscountId,@ReceiptId,@DateEntered,@DiscountCodeId,@TypeId,@DiscountAmount,@DiscountPercentage," +
                      "@MinSpend,@MaxSpend,@ValidFrom,@ValidTo,@TotalCustomerCount,@AppliesToSaleItems,@SingleUse,@DiscountTotal,@DateModified) " +
                      "Set Identity_insert ReceiptDiscount OFF ";

            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(sql, new
                {
                    @ReceiptDiscountId = receiptDiscountId,
                    @ReceiptId = receiptId,
                    @DateEntered = _dateTimeProvider.Now,
                    @DiscountCodeId = 12345,
                    @TypeId = 0,
                    @DiscountAmount = disAmount,
                    @DiscountPercentage = percentageDiscount,
                    @MinSpend = (decimal)10,
                    @MaxSpend = (decimal)25,
                    @ValidFrom = _dateTimeProvider.Now.AddDays(-2),
                    @ValidTo = _dateTimeProvider.Now.AddDays(2),
                    @TotalCustomerCount = 0,
                    @AppliesToSaleItems = 1,
                    @SingleUse = 0,
                    @DiscountTotal = discountTotal,
                    @DateModified = _dateTimeProvider.Now.AddMinutes(-45)
                });
            }
        }

        public static void InsertSubscriptionForNserviceBus(string subscription, string typeName, string version)
        {
            var insertSubscription =
                "delete from Subscription where SubscriberEndpoint = @SubscriberEndpoint; " +
                "Insert Into [Subscription] " +
                "values(@SubscriberEndpoint,@MessageType,@Version,@TypeName);";
            var connectionString = ConfigurationManager.ConnectionStrings["NServiceBus/Persistence"].ConnectionString;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Execute(insertSubscription, new
                {
                    @SubscriberEndpoint = subscription,
                    @MessageType = string.Concat(typeName, ",", version),
                    @Version = version,
                    @TypeName = typeName
                });
            }
        }

        public void BuiltInventoryInDatabase(int inventoryId, decimal price, int externalId, string sKU)
        {
            //buildInventory in Backoffice
            ClearInventoryData(inventoryId, _connectionStringBackoffice);
            InsertIntoInventoryTable(inventoryId, price, externalId, sKU, _connectionStringBackoffice);

            //buildInventory in Checkout
            // ClearInventoryData(inventoryId,
            // ConfigurationManager.ConnectionStrings["AsosCheckOutConnectionString"].ConnectionString);
            InsertIntoInventoryTable(inventoryId, price, externalId, sKU, ConfigurationManager.ConnectionStrings["AsosCheckOutConnectionString"].ConnectionString);


            //  SetupInventoryReferenceData(inventoryId, price);
        }

        public void InsertIntoInventoryStock(int inventoryId, int inStock, int allocated, string sKU)
        {
            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosCheckOutConnectionString"].ConnectionString))
            {
                var sql = "delete InventoryStock from InventoryStock " +
                          "Join Inventory ON Inventory.InventoryId = InventoryStock.InventoryId where Inventory.SKU = @sKU " +
                          "insert into InventoryStock(InventoryId,InStock,Reserved,Allocated,DateLastRefreshed,DateEntered,DateModified) " +
                          "values(@InventoryId,@InStock,@Reserved,@Allocated,@DateLastRefreshed,@DateEntered,@DateModified) ";
                conn.Execute(sql, new
                {
                    @InventoryId = inventoryId,
                    @InStock = inStock,
                    @Reserved = 0,
                    @Allocated = allocated,
                    @DateLastRefreshed = _dateTimeProvider.Now,
                    @DateEntered = _dateTimeProvider.Now,
                    @DateModified = _dateTimeProvider.Now,
                    @sKU = sKU
                });
            }
        }

        public void InsertIntoStockAllocationDbForWarehouse(string sKU, int inStock, int allocated, string externalWarehouseId)
        {
            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosStockAllocationConnectionString"].ConnectionString))
            {
                var sql = "delete from Event where SKU = @SKU " +
                          "delete from StockLevel where SKU = @SKU and WarehouseId = @WarehouseId " +
                         "insert into StockLevel(SKU,Instock,Allocated,WarehouseId,LastModified) " +
                          "values(@SKU,@Instock,@Allocated,@WarehouseId,@LastModified)";
                conn.Execute(sql, new
                {
                    @SKU = sKU,
                    @Instock = inStock,
                    @Allocated = allocated,
                    @WarehouseId = externalWarehouseId,
                    @LastModified = _dateTimeProvider.Now
                });
            }
        }

        public static void InsertIntoCustomerTable(int customerId, string email, string firstName, string lastName)
        {
            var sql = "delete from Customer where CustomerId = @CustomerId " +
                      "Set identity_insert Customer ON " +
                      "insert into Customer (CustomerId,DateEntered,DateModified,CustomerGUID,FirstName,LastName," +
                      "Email,DOB,Gender,StatusId,Password,PasswordExpiryDate,BlockedDateTime,FailedLoginCount," +
                      "DisplayName,msrepl_tran_version,SiteId,IsFirstTimeBuyer) " +
                      "values (@CustomerId,@DateEntered,@DateModified,@CustomerGUID,@FirstName,@LastName," +
                      "@Email,@DOB,@Gender,@StatusId,@Password,@PasswordExpiryDate,@BlockedDateTime,@FailedLoginCount," +
                      "@DisplayName,@msrepl_tran_version,@SiteId,@IsFirstTimeBuyer)" +
                      "Set identity_insert Customer OFF ";
            string Null = null;
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["BackofficeDb"].ConnectionString))
            {
                connection.Open();
                connection.Execute(sql, new
                {
                    @CustomerId = customerId,
                    @DateEntered = DateTime.Now,
                    @DateModified = DateTime.Now,
                    @CustomerGUID = "abcsd123455",
                    @FirstName = firstName,
                    @LastName = lastName,
                    @Email = email,
                    @DOB = DateTime.Now.AddYears(-20),
                    @Gender = 'M',
                    @StatusId = 0,
                    @Password = "lsnLoZXlsRPk",
                    @PasswordExpiryDate = Null,
                    @BlockedDateTime = Null,
                    @FailedLoginCount = 0,
                    @DisplayName = firstName,
                    @msrepl_tran_version = new Guid("E5F526C7-02E5-495F-84F4-461815539952"),
                    @SiteId = 1,
                    @IsFirstTimeBuyer = 0
                });
            }
        }

        public void InsertIntoInventoryTable(int inventoryId, decimal price, int externalId, string sKU, string connectionString)
        {
            var sql = "delete from Inventory where InventoryId = @InventoryId; " +
                      "Set identity_insert Inventory ON " +
                      "INSERT INTO Inventory(InventoryId,ParentID,SKU,SKUAlt,StatusID,PreviousStatusID,SupplierID,BrandId,AdditionalInfo,ShortDescription," +
                      "LongDescription,InventoryTitle,InventoryURL,Returnable,CareInfo,ClassificationID,Colour,ColourID,SizeID,CostPrice,OriginalRetailPrice," +
                      "CurrentPrice,PreviousPrice,SupplierRef,Weight,TaxSetupID,Width,Height,Length,Materials,GoLive,DateEntered,ExpROSWeekly,DateOnSite," +
                      "ShippingRestrictionId,FraudRiskTypeId,InventorySizeASOSId,ReasonId,WorkflowId,RRP,CountryOfOriginId,DateModified,FabricConstruction," +
                      "CommodityCode,Hazmat,HazmatId,InventorySizeTextId,BackOfficeStatusDateModified,ColourCodeId,CareCode,PrimaryEAN,AverageUnitCost," +
                      "MerchandisingSeason,PackingFormat,FreightProductType,ExternalId,ExternalSupplierNumber,BackOfficeStatusID,MerretStatusID,PreviousMerretStatusID," +
                      "PutLiveBy,SupplierUnitCost,SupplierCurrency,TicketSupplier,QueryStatusDateModified,ManufacturerStyleID)" +
                      "VALUES(@InventoryId,@ParentID,@SKU,@SKUAlt,@StatusID,@PreviousStatusID,@SupplierID,@BrandId,@AdditionalInfo,@ShortDescription,@LongDescription," +
                      "@InventoryTitle,@InventoryURL,@Returnable,@CareInfo,@ClassificationID,@Colour,@ColourID,@SizeID,@CostPrice,@OriginalRetailPrice,@CurrentPrice," +
                      "@PreviousPrice,@SupplierRef,@Weight,@TaxSetupID,@Width,@Height,@Length,@Materials,@GoLive,@DateEntered,@ExpROSWeekly,@DateOnSite,@ShippingRestrictionId," +
                      "@FraudRiskTypeId,@InventorySizeASOSId,@ReasonId,@WorkflowId,@RRP,@CountryOfOriginId,@DateModified,@FabricConstruction,@CommodityCode,@Hazmat," +
                      "@HazmatId,@InventorySizeTextId,@BackOfficeStatusDateModified,@ColourCodeId,@CareCode,@PrimaryEAN,@AverageUnitCost,@MerchandisingSeason,@PackingFormat," +
                      "@FreightProductType,@ExternalId,@ExternalSupplierNumber,@BackOfficeStatusID,@MerretStatusID,@PreviousMerretStatusID,@PutLiveBy,@SupplierUnitCost," +
                      "@SupplierCurrency,@TicketSupplier,@QueryStatusDateModified,@ManufacturerStyleID)" +
                      "Set Identity_insert Inventory OFF";

            using (var connection = new SqlConnection(connectionString))
            {
                string NULL = null;
                connection.Open();
                connection.Execute("delete from Inventory where SKU = @SKU",
                   new { @SKU = sKU });
                connection.Execute(sql, new
                {
                    @InventoryId = inventoryId,
                    @ParentID = inventoryId,
                    @SKU = sKU,
                    @SKUAlt = "TS",
                    @StatusID = 52000,
                    @PreviousStatusID = 0,
                    @SupplierID = 123,
                    @BrandId = 12,
                    @AdditionalInfo = "Test SKU",
                    @ShortDescription = "Test SKU",
                    @LongDescription = "Test SKU",
                    @InventoryTitle = "Test SKU",
                    @InventoryURL = "/Bourjois/Bourjois-Round-Eyeshadow/Prod/pgeproduct.aspx?iid=3530",
                    @Returnable = 1,
                    @CareInfo = NULL,
                    @ClassificationID = NULL,
                    @Colour = NULL,
                    @ColourID = 12,
                    @SizeID = 12,
                    @CostPrice = 1,
                    @OriginalRetailPrice = 2,
                    @CurrentPrice = price,
                    @PreviousPrice = 1,
                    @SupplierRef = NULL,
                    @Weight = NULL,
                    @TaxSetupID = 1,
                    @Width = NULL,
                    @Height = NULL,
                    @Length = NULL,
                    @Materials = NULL,
                    @GoLive = NULL,
                    @DateEntered = _dateTimeProvider.Now,
                    @ExpROSWeekly = 0,
                    @DateOnSite = NULL,
                    @ShippingRestrictionId = 1,
                    @FraudRiskTypeId = 0,
                    @InventorySizeASOSId = NULL,
                    @ReasonId = NULL,
                    @WorkflowId = 1,
                    @RRP = NULL,
                    @CountryOfOriginId = 1,
                    @DateModified = _dateTimeProvider.Now,
                    @FabricConstruction = NULL,
                    @CommodityCode = NULL,
                    @Hazmat = NULL,
                    @HazmatId = NULL,
                    @InventorySizeTextId = NULL,
                    @BackOfficeStatusDateModified = NULL,
                    @ColourCodeId = NULL,
                    @CareCode = NULL,
                    @PrimaryEAN = NULL,
                    @AverageUnitCost = price,
                    @MerchandisingSeason = NULL,
                    @PackingFormat = NULL,
                    @FreightProductType = NULL,
                    @ExternalId = externalId,
                    @ExternalSupplierNumber = "",
                    @BackOfficeStatusID = 0,
                    @MerretStatusID = 0,
                    @PreviousMerretStatusID = NULL,
                    @PutLiveBy = NULL,
                    @SupplierUnitCost = NULL,
                    @SupplierCurrency = NULL,
                    @TicketSupplier = NULL,
                    @QueryStatusDateModified = NULL,
                    @ManufacturerStyleID = NULL
                });
            }
        }

        private void InsertIntoReceiptSiteId(int receiptId)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                connection.Execute("IF not exists (Select * from ReceiptSite where ReceiptSiteId = @ReceiptSiteId) " +
                                   "BEGIN " +
                                   "Set identity_insert ReceiptSite ON " +
                                   "Insert into ReceiptSite(ReceiptSiteId,ReceiptId,SiteId) " +
                                   "Values(@ReceiptSiteId,@ReceiptId,@SiteId) " +
                                   "Set identity_insert ReceiptSite OFF " +
                                   "END ", new
                                   {
                                       @ReceiptSiteId = 789,
                                       @ReceiptId = receiptId,
                                       @SiteId = 1
                                   });
            }
        }

        public void InsertIntoVoidTablesForCancellation(int receiptId, int receiptItemId, int quantity)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                var voidIheader = InsertIntoVoidheader(receiptId, connection);
                InsertIntoVoidItem(receiptItemId, voidIheader.VoidHeaderId, quantity, connection);
            }
        }

        private dynamic InsertIntoVoidheader(int receiptId, SqlConnection connection)
        {
            String Null = null;
            var sql = "If not exists(select receiptId from Voidheader where receiptId = @receiptId) " +
                      "BEGIN " +
                      @"Insert Into VoidHeader values(@ReceiptId,@StatusId,@DateEntered,@DateModified,@EnteredByUserId," +
                      "@ReceivedByUserId,@ReplacementReceiptId,@Reason,@ReceiptVoucherId) " +
                      "END ";
            connection.Execute(sql,
                               new
                               {
                                   @ReceiptId = receiptId,
                                   @StatusId = 20000,
                                   @DateEntered = _dateTimeProvider.Now,
                                   @DateModified = _dateTimeProvider.Now,
                                   @EnteredByUserId = 100,
                                   @ReceivedByUserId = 100,
                                   @ReplacementReceiptId = Null,
                                   @Reason = Null,
                                   @ReceiptVoucherId = Null
                               });
            return connection.Query<dynamic>("select * from Voidheader where receiptId = @receiptId",
                                             new
                                             {
                                                 receiptId
                                             }).First();


        }

        private void InsertIntoVoidItem(int receiptItemId, int voidheaderId, int quantity, SqlConnection connection)
        {
            String Null = null;
            var receiptItem = GetReceiptItemDetailsForReceiptItem(receiptItemId);
            var sql = "Insert into VoidItem " +
                      "Values (@VoidHeaderId,@ReceiptItemId,@ReceiptDropId,@Quantity,@DateEntered," +
                      "@EnteredByActionId,@EnteredByReasonId,@ReceivedByActionId,@ReceivedByReasonId,@UnaccountedCreditAmount," +
                      "@VoidFaultId,@InventoryId,@LPN,@PurchaseItemId,@ReceiptVoucherId,@OutstandingReturn,@DateModified,@ReceiptShippingSubscriptionId)";
            connection.Execute(sql, new
            {
                @VoidHeaderId = voidheaderId,
                @ReceiptItemId = receiptItemId,
                @ReceiptDropId = Null,
                @Quantity = quantity,
                @DateEntered = _dateTimeProvider.Now,
                @EnteredByActionId = 4,
                @EnteredByReasonId = 20,
                @ReceivedByActionId = 4,
                @ReceivedByReasonId = 20,
                @UnaccountedCreditAmount = 0,
                @VoidFaultId = Null,
                @InventoryId = (int)receiptItem.InventoryId,
                @LPN = -1,
                @PurchaseItemId = Null,
                @ReceiptVoucherId = Null,
                @OutstandingReturn = 0,
                @DateModified = _dateTimeProvider.Now,
                @ReceiptShippingSubscriptionId = Null
            });
        }


        public void SetupInventoryReferenceData(int inventoryId, decimal price)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                string NULL = null;
                connection.Open();
                var hierarchyNodeId =
                    connection.Query<Int16>("Select MAX(HierarchyNodeId) from bm.HierarchyNode").SingleOrDefault() + 1;
                var bulletInLineId =
                    connection.Query<int?>("Select MAX(bulletinlineId) from BulletInline").SingleOrDefault() ?? 0;
                var bulletinId =
                    connection.Query<int?>("Select MAX(bulletinId) from BulletIn").SingleOrDefault() ?? 0;
                connection.Execute("Set identity_insert bm.HierarchyNode ON " +
                                 "insert into bm.HierarchyNode(HierarchyNodeId,HierarchyLevelId,ParentId,Reference,Description,ExternalId) " +
                                 "values(@HierarchyNodeId,@HierarchyLevelId,@ParentId,@Reference,@Description,@ExternalId) " +
                                 "Set Identity_insert bm.HierarchyNode OFF", new
                                 {
                                     @HierarchyNodeId = hierarchyNodeId,
                                     @HierarchyLevelId = 3,
                                     @ParentId = hierarchyNodeId,
                                     @Reference = NULL,
                                     @Description = "asos.com",
                                     @ExternalId = 001
                                 });
                connection.Execute("insert into bm.InventoryHierarchyNode(InventoryId,HierarchyNodeId,DateModified)" +
                                 "values(@InventoryId,@HierarchyNodeId,@DateModified)", new
                                 {
                                     @InventoryId = inventoryId,
                                     @HierarchyNodeId = hierarchyNodeId,
                                     @DateModified = _dateTimeProvider.Now
                                 });
                connection.Execute("Set identity_insert bulletin On " +
                                   "Insert into Bulletin(BulletinId,Description,BulletinStatusId,MarkdownEventId,DateCreated,CreatedBy,DateModified," +
                                   "ModifiedBy,StartDateTime,EndDateTime,ExternalId,MarkdownTypeId) " +
                                   "values(@BulletinId,@Description,@BulletinStatusId,@MarkdownEventId,@DateCreated,@CreatedBy,@DateModified," +
                                   "@ModifiedBy,@StartDateTime,@EndDateTime,@ExternalId,@MarkdownTypeId) " +
                                   "Set identity_insert bulletin OFF ", new
                                   {
                                       @BulletinId = bulletinId + 1,
                                       @Description = "TEST",
                                       @BulletinStatusId = 9,
                                       @MarkdownEventId = 12,
                                       @DateCreated = _dateTimeProvider.Now,
                                       @CreatedBy = 12,
                                       @DateModified = _dateTimeProvider.Now,
                                       @ModifiedBy = -100,
                                       @StartDateTime = _dateTimeProvider.Now.AddDays(-3),
                                       @EndDateTime = _dateTimeProvider.Now.AddDays(3),
                                       @ExternalId = NULL,
                                       @MarkdownTypeId = 2
                                   });
                connection.Execute("Set Identity_insert BulletInline ON " +
                                 "insert into BulletInLine(bulletinlineid,bulletinId,inventoryId,ProposedPrice,BulletinLineStatusID,Colour)" +
                                 "values(@bulletinlineid,@bulletinId,@inventoryId,@ProposedPrice,@BulletinLineStatusID,@Colour)" +
                                 "Set Identity_insert Bulletinline OFF", new
                                 {
                                     @bulletinlineid = bulletInLineId + 1,
                                     @bulletinId = bulletinId + 1,
                                     @inventoryId = inventoryId,
                                     @ProposedPrice = price,
                                     @BulletinLineStatusID = 2,
                                     @Colour = "BE1"
                                 });
                var sql =
                    string.Format(
                        "insert into bm.HierarchyNodeSetting(HierarchyNodeId,SecurityGroupId,ShippingRestrictionId,AllowDifferentialPricing," +
                        " DefaultTaxSetupId,WarehouseId,MaxDiscountPercentage,ShowVideo,FraudRisk,DateEntered,DateModified)" +
                        " values ({0},46,1,0,1,2,40,1,1,GETDATE(),GETDATE())", hierarchyNodeId);
                connection.Execute(sql);
            }
        }

        public void UpdateReceiptDropForShippingMethod(string shippingMethod, int receiptId, bool deliverToStore)
        {
            var shippingMethods = GetShippingMethodTable();
            string sql = string.Empty;
            if (deliverToStore)
            {
                foreach (var ship in shippingMethods.Where((x) => x.MethodName == shippingMethod && x.DeliverToStoreId != null))
                {
                    sql = string.Format(@"Update ReceiptDrop Set ShippingMethodId = {0} where receiptId = {1}",
                                        ship.ShippingMethodId, receiptId);
                }
            }
            else
            {
                foreach (var ship in shippingMethods.Where((x) => x.MethodName == shippingMethod && x.DeliverToStoreId == null))
                {
                    sql = string.Format(@"Update ReceiptDrop Set ShippingMethodId = {0} where receiptId = {1}",
                                        ship.ShippingMethodId, receiptId);
                }
            }
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(sql);
            }
        }

        public void UpdateReceiptItemForDiscount(int receiptItemId, decimal percentageDiscount, bool discountExempt)
        {

            byte disExemp = discountExempt == true ? (byte)1 : (byte)0;

            var sql = @"update ReceiptItem set PercentDiscountAvailable = @percentageDiscount,
                     DiscountExempt = @discountExempt where receiptItemId = @receiptItemId";
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(sql, new
                {
                    @receiptItemId = receiptItemId,
                    @percentageDiscount = percentageDiscount,
                    @discountExempt = disExemp
                });
            }
        }

        public void UpdateReceiptItemForQuantity(int receiptItemId, int quantity)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute("update ReceiptItem set Quantity = @quantity where ReceiptItemId = @receiptItemId ",
                                   new
                                   {
                                       quantity,
                                       receiptItemId
                                   });
            }
        }

        public void UpdateReceiptStatusIdForReceipt(int receiptId)
        {
            var sql = "Update Receipt set statusId = 47000 where receiptId = @receiptId";
            using(var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(sql, new {receiptId});
            }
        }

        public void UpdateCurrencyForReceipt(int receiptId,string countryName)
        {
            var sql = @"declare @CountryId int
                       declare @ExchangeRate decimal(9,5) 
                      select @CountryId = CountryId,@ExchangeRate = ExchangeRate from Country
                      where CountryName = @countryName
                     update Receipt set CurrencyId = @CountryId,ExchangeRate = @ExchangeRate where ReceiptId = @receiptId";

            using(var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(sql,
                                   new
                                       {
                                           countryName,
                                           receiptId
                                       });
            }
        }

        public int GetOrderStatus(int receiptId)
        {
            var sql = "select statusId from receipt where receiptId = @receiptId";

            using(var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return connection.Query<int>(sql, new {receiptId}).First();
            }
        }

        public IEnumerable<dynamic> GetShippingMethodTable()
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return connection.Query("select * from ShippingMethod").ToList();
            }
        }

        public void ClearInventoryData(int inventoryId, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var hierarchyNodeId =
                   connection.Query<Int16>(
                       "Select HierarchyNodeId from bm.InventoryHierarchyNode where inventoryId = @inventoryId",
                       new { inventoryId }).SingleOrDefault();
                var tables = new[]
                {
                    "bm.InventoryHierarchyNode",
                    "dbo.InventoryMarkdownHistory",
                    "dbo.Bulletinline",
                    "dbo.InventoryStock"
                };

                foreach (var table in tables)
                {
                    var sql = string.Format("delete from {0} where inventoryId = '{1}'", table, inventoryId);
                    connection.Execute(sql);
                }
                if (hierarchyNodeId != 0)
                    connection.Query<int>("delete from bm.HierarchyNodeSetting where hierarchyNodeId = @hierarchyNodeId " +
                                           "delete from bm.HierarchyNode where hierarchyNodeId = @hierarchyNodeId",
                        new { hierarchyNodeId });
                connection.Execute("delete from Inventory where inventoryId = @inventoryId",
                  new { inventoryId });
            }
        }

        public void ClearWmsDespatchAdviceQueueForScenario()
        {
            var deleteExistingRecords = "delete from WmsDespatchAdviceQueue ";
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Execute(deleteExistingRecords);
            }
        }

        public void ClearTestDataForTestExecution(int receiptId)
        {
            DeleteResultCacheInStockAllocationDb(receiptId);
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                ClearPosItemTables(receiptId, connection);
                ClearReceiptItemTables(receiptId, connection);
                ClearReceiptTables(receiptId, connection);
                ClearVoidItemTables(receiptId, connection);
            }
        }

        public void ClearLocalDataForTestExecution(int receiptId)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                connection.Open();
                ClearPosItemTables(receiptId, connection);
                ClearReceiptItemTables(receiptId, connection);
                ClearReceiptTables(receiptId, connection);
                ClearVoidItemTables(receiptId, connection);
            }
        }

        public void DeleteResultCacheInStockAllocationDb(int receiptId)
        {

            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosStockAllocationConnectionString"].ConnectionString))
            {
                var sql = "delete from ResultCache where CorrelationId = @CorrelationId ";

                conn.Execute(sql, new
                {
                    @CorrelationId = receiptId.ToString()
                });
            }
        }

        private void ClearPosItemTables(int receiptId, SqlConnection connection)
        {
            var positem = connection.Query<int>("Select positemId from positem where receiptId = @receiptId",
                      new { receiptId }).ToList();
            var sql = string.Format("delete from positemWarehouse where positemId = @positemId");
            foreach (var positemId in positem)
            {
                connection.Execute(sql, new
                {
                    positemId
                });
            }
        }

        private void ClearReceiptItemTables(int receiptId, SqlConnection connection)
        {
            var receiptitem = connection.Query<int>("Select receiptitemId from receiptitem where receiptId = @receiptId",
                      new { receiptId }).ToList();
            var sql1 = string.Format("delete from receiptItemMarkdown where receiptitemId = @receiptitemId");
            foreach (var receiptitemId in receiptitem)
            {
                connection.Execute(sql1, new
                {
                    @receiptitemId = receiptitemId
                });
            }
        }

        private void ClearReceiptTables(int receiptId, SqlConnection connection)
        {
            var tables = new[]
                    {
                        "positem",                        
                        "receiptDrop",
                        "receiptItem",
                        "receiptSite",
                        "receipt",
                        "ReceiptStatusHistory",
                        "WmsDespatchAdviceQueue",
                        "ReceiptStatusHistory",
                        "ReceiptComment",
                        "ReceiptDiscount"
                    };
            foreach (var table in tables)
            {
                var sql = string.Format("delete from {0} where receiptId = @receiptId", table);
                connection.Execute(sql, new { receiptId });
            }
        }

        private void ClearVoidItemTables(int receiptId, SqlConnection connection)
        {
            var sql = "delete VoidItem from VoidItem vi " +
                      "Join voidheader vh ON vh.VoidheaderId = vi.VoidheaderId  " +
                      "where vh.ReceiptId = @receiptId " +
                      "delete from Voidheader where receiptId = @receiptId";
            connection.Execute(sql, new
            {
                receiptId
            });
        }

        public void ClearStockFromStockLevelTableForAWarehouse(string[] sKus, string warehouseName)
        {
            var sKUs = BuildValuesForSqlStatement(sKus);
            foreach (var warehouse in GetWarehouseTable().Where(warehouse => warehouse.Reference == warehouseName))
            {
                var sql = string.Format(@"delete from StockLevel where SKU in ({0}) and WarehouseId = '{1}'", sKUs, warehouse.ExternalWarehouseId).ToString();
                using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosStockAllocationConnectionString"].ConnectionString))
                {
                    connection.Execute((string)sql);
                }
            }

        }

        public void ClearStockFromStockLevelTableForAllWarehouse(string[] sKus)
        {
            var sKUs = BuildValuesForSqlStatement(sKus);
            var sql = string.Format(@"delete from Event where SKU in ({0}) 
                                          delete from StockLevel where SKU in ({0})", sKUs).ToString();
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosStockAllocationConnectionString"].ConnectionString))
            {
                connection.Execute((string)sql);
            }


        }

        public void ClearInventoryStockForSKU(string[] sKUs)
        {
            var skus = BuildValuesForSqlStatement(sKUs);
            var sql =
                string.Format(
                    @"delete InventoryStock from InventoryStock ist 
                     join inventory i on i.inventoryId = ist.InventoryId 
                     where i.SKU in ({0})",
                    skus);
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosCheckOutConnectionString"].ConnectionString))
            {
                connection.Execute((string)sql);
            }
        }

/*        public void ClearCustomerData(IEnumerable<string> servers)
        {
            foreach (var item in servers)
            {
                var procStartInfo =
                    new ProcessStartInfo("cmd", "/c " + "iisreset " + item)
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                var proc = new Process {StartInfo = procStartInfo};
                proc.Start();
                Console.WriteLine("Deleting Customer");
            }
            Thread.Sleep(10000);
        }*/

        private string GetMerretExternalSupplierNumber()
        {
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["AsosReportingConnectionString"].ConnectionString))
            {
                connection.Open();
                return connection.Query<string>("Select SupplierId from MerretReport.Persistence.Supplier").First();
            }
        }

        public IEnumerable<dynamic> GetWarehouseTable()
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return connection.Query("select * from Warehouse").ToList();
            }
        }

        public dynamic GetInventoryDetailsForSku(String sKU)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return connection.Query("select InventoryId,AverageUnitCost from inventory where SKU = @sKU", new { sKU }).First();
            }
        }

        public dynamic GetReceiptItemDetailsForReceiptItem(int? receiptItemId)
        {
            using (var connection = new SqlConnection(_connectionStringBackoffice))
            {
                return
                    connection.Query("select InventoryId,PriceIncTax,SKU,Tax from receiptItem where receiptItemId = @receiptItemId",
                                     new { receiptItemId }).First();
            }
        }

        public int ClearResultCache(int orderId)
        {
            const string sql = "delete ResultCache where CorrelationId = @orderId; " +
                               "select @@rowcount as ResultCount";
            using(var connection = new SqlConnection(_connectionStringStockAllocation))
            {
                return (connection.Query(sql, new { @orderId = orderId.ToString() }).FirstOrDefault() ?? 0).ResultCount;
            }
        }

        public int ClearEvent(int orderId)
        {
            const string sql = "delete Event where CorrelationId = @orderId; " +
                               "select @@rowcount as ResultCount";
            using(var connection = new SqlConnection(_connectionStringStockAllocation))
            {
                return (connection.Query(sql, new { @orderId = orderId.ToString() }).FirstOrDefault() ?? 0).ResultCount;
            }
        }

        private string BuildValuesForSqlStatement(int[] values)
        {

            var builder = new StringBuilder();
            foreach (var value in values)
            {
                if (values.Last() == value)
                {
                    builder.Append("'" + value + "'");
                    break;
                }
                builder.Append("'" + value + "',");
            }
            return builder.ToString();
        }

        private string BuildValuesForSqlStatement(string[] values)
        {

            var builder = new StringBuilder();
            foreach (var value in values)
            {
                if (values.Last() == value)
                {
                    builder.Append("'" + value + "'");
                    break;
                }
                builder.Append("'" + value + "',");
            }
            return builder.ToString();
        }
    }

}
