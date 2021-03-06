using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.QueryFilter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Intuit.Ipp.DataService;

namespace MvcCodeFlowClientManual.Controllers
{
    public class BillingController : AppController
    {
        // GET: Customer
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Workflow to create Vendor, Bill, VendorCredit, BillPayment 
        /// </summary>
        public async Task<ActionResult> BillingWorkflow()
        {
            //Make QBO api calls using .Net SDK
            if (Session["realmId"] != null)
            {
                string realmId = Session["realmId"].ToString();

                try
                {

                    //Initialize OAuth2RequestValidator and ServiceContext
                    ServiceContext serviceContext = base.IntializeContext(realmId);


                    //Create a Vendor
                    Vendor vendor = CreateVendor(serviceContext);
                    //Add Bill for this Vendor
                    Bill bill = CreateBill(serviceContext, vendor);
                    //Add BillPayment for this Vendor
                    BillPayment billPayment = CreateBillPaymentCreditCard(serviceContext, vendor, bill);
                    //Create & Apply Vendor Credit
                    VendorCredit vendorCredit = CreateVendorCredit(serviceContext, vendor);

                    return View("Index", (object)("QBO API calls Success!"));
                }
                catch (Exception ex)
                {
                    return View("Index", (object)"QBO API calls Failed!");
                }

            }
            else
                return View("Index", (object)"QBO API call Failed!");
        }

        #region create vendor
        /*
         * Create a Vendor
         * The Vendor object represents the seller from whom your company purchases any service or product. 
         */
        private Vendor CreateVendor(ServiceContext serviceContext)
        {
            DataService dataService = new DataService(serviceContext);
            Vendor vendor = new Vendor();


            //Add additional vendor info

            vendor.GivenName = "GivenName";
            vendor.MiddleName = "MiddleName";
            vendor.FamilyName = "FamilyName";
            vendor.Suffix = "Suffix";
            vendor.CompanyName = "CompanyName";
            vendor.DisplayName = "DisplayName_" + Guid.NewGuid().ToString("N");
      
            vendor.Active = true;
            vendor.ActiveSpecified = true;

            //Add contact details ie phone, fax, email and website details
            TelephoneNumber primaryPhone = new TelephoneNumber();
            primaryPhone.FreeFormNumber = "FreeFormNumber";
            vendor.PrimaryPhone = primaryPhone;

            TelephoneNumber alternatePhone = new TelephoneNumber();
            alternatePhone.FreeFormNumber = "FreeFormNumber";
            vendor.AlternatePhone = alternatePhone;

            TelephoneNumber mobile = new TelephoneNumber();
            mobile.FreeFormNumber = "FreeFormNumber";
            vendor.Mobile = mobile;

            TelephoneNumber fax = new TelephoneNumber();
            fax.FreeFormNumber = "FreeFormNumber";
            vendor.Fax = fax;

            EmailAddress primaryEmailAddr = new EmailAddress();
            primaryEmailAddr.Address = "Address@add.com";
            vendor.PrimaryEmailAddr = primaryEmailAddr;

            WebSiteAddress webAddr = new WebSiteAddress();
            webAddr.URI = "http://site.com";
            vendor.WebAddr = webAddr;
            Vendor vendorAdded = dataService.Add<Vendor>(vendor);
            return vendorAdded;
        }
        #endregion

        #region create bill
        /*
         * Create's a Bill
         * A Bill object is an AP transaction representing a request-for-payment from a third party for goods/services rendered, received, or both. 
         */
        private Bill CreateBill(ServiceContext serviceContext, Vendor vendors)
        {
            DataService dataService = new DataService(serviceContext);
            #region create customer
            Random random = new Random();
            Customer customer = new Customer();

            customer.GivenName = "Bob" + random.Next();
            customer.FamilyName = "Serling";
            customer.DisplayName = customer.CompanyName;
            Customer customeradded = dataService.Add<Customer>(customer);

            #endregion

            #region create liability account

            //Get a liability account. If not present create one
            QueryService<Account> accountQuerySvc = new QueryService<Account>(serviceContext);
            Account liabilityAccount = accountQuerySvc.ExecuteIdsQuery("SELECT * FROM Account WHERE AccountType='Accounts Payable' AND Classification='Liability'").FirstOrDefault();
            if (liabilityAccount == null)
            {
                Account accountp = new Account();
                String guid = Guid.NewGuid().ToString("N");
                accountp.Name = "Name_" + guid;

               // accountp.FullyQualifiedName = liabilityAccount.Name;

                accountp.Classification = AccountClassificationEnum.Liability;
                accountp.ClassificationSpecified = true;
                accountp.AccountType = AccountTypeEnum.AccountsPayable;
                accountp.AccountTypeSpecified = true;

                accountp.CurrencyRef = new ReferenceType()
                {
                    name = "United States Dollar",
                    Value = "USD"
                };
                liabilityAccount = dataService.Add<Account>(accountp);
            }
            #endregion

            #region create expense account
            //Get a Expense account. If not present create one
            Account expenseAccount = accountQuerySvc.ExecuteIdsQuery("SELECT * FROM Account WHERE AccountType='Expense' AND Classification='Expense'").FirstOrDefault();
            if (expenseAccount == null)
            {
                Account accounte = new Account();
                String guid = Guid.NewGuid().ToString("N");
                accounte.Name = "Name_" + guid;

               // accounte.FullyQualifiedName = expenseAccount.Name;

                accounte.Classification = AccountClassificationEnum.Liability;
                accounte.ClassificationSpecified = true;
                accounte.AccountType = AccountTypeEnum.AccountsPayable;
                accounte.AccountTypeSpecified = true;

                accounte.CurrencyRef = new ReferenceType()
                {
                    name = "United States Dollar",
                    Value = "USD"
                };
                expenseAccount = dataService.Add<Account>(accounte);
            }
            #endregion

            #region create bill for the added vendor
            //Create a bill and add a vendor reference
            Bill bill = new Bill();

            bill.DueDate = DateTime.UtcNow.Date;
            bill.DueDateSpecified = true;
            bill.VendorRef = new ReferenceType()
            {

                name = vendors.DisplayName,
                Value = vendors.Id
            };
            bill.APAccountRef = new ReferenceType()
            {

                name = liabilityAccount.Name,
                Value = liabilityAccount.Id
            };
            bill.TotalAmt = new Decimal(100.00);
            bill.TotalAmtSpecified = true;
            bill.Balance = new Decimal(100.00);
            bill.BalanceSpecified = true;
            bill.TxnDate = DateTime.UtcNow.Date;
            bill.TxnDateSpecified = true;

            //Create a line for the bill
            List<Line> lineList = new List<Line>();
            Line line = new Line();
            line.Description = "Description";
            line.Amount = new Decimal(100.00);
            line.AmountSpecified = true;
            line.DetailType = LineDetailTypeEnum.AccountBasedExpenseLineDetail;
            line.DetailTypeSpecified = true;
            lineList.Add(line);
            bill.Line = lineList.ToArray();

            //Create an AccountBasedExpenseLineDetail
            AccountBasedExpenseLineDetail detail = new AccountBasedExpenseLineDetail();
            detail.CustomerRef = new ReferenceType { name = customeradded.DisplayName, Value = customeradded.Id };
            detail.AccountRef = new ReferenceType { name = expenseAccount.Name, Value = expenseAccount.Id };
            detail.BillableStatus = BillableStatusEnum.NotBillable;
            line.AnyIntuitObject = detail;
            Bill billadded = dataService.Add<Bill>(bill);
            return billadded;
            #endregion
        }
        #endregion

        #region create vendor credit

        /*
         * Creates a VendorCredit 
         * The VendorCredit object is an accounts payable transaction that represents a refund or credit of payment for goods or services. 
         * It is a credit that a vendor owes you for various reasons such as overpaid bill, returned merchandise, or other reasons.
         */
        private VendorCredit CreateVendorCredit(ServiceContext serviceContext, Vendor vendor)
        {
            DataService dataService = new DataService(serviceContext);
            QueryService<Account> accountQuerySvc = new QueryService<Account>(serviceContext);
            //Create a VendorCredut and add a reference to a Vendor 
            VendorCredit vendorCredit = new VendorCredit();
            vendorCredit.VendorRef = new ReferenceType()
            {
                name = vendor.DisplayName,
                Value = vendor.Id
            };

            //Create a Liability Account and add a reference to the Vendor Credit. If not present create a  Liability Account
            #region create liability account

            //Get a liability account. If not present create one
          
            Account liabilityAccount = accountQuerySvc.ExecuteIdsQuery("SELECT * FROM Account WHERE AccountType='Accounts Payable' AND Classification='Liability'").FirstOrDefault();
            if (liabilityAccount == null)
            {
                Account accountp = new Account();
                String guid = Guid.NewGuid().ToString("N");
                accountp.Name = "Name_" + guid;

                //accountp.FullyQualifiedName = liabilityAccount.Name;

                accountp.Classification = AccountClassificationEnum.Liability;
                accountp.ClassificationSpecified = true;
                accountp.AccountType = AccountTypeEnum.AccountsPayable;
                accountp.AccountTypeSpecified = true;

                accountp.CurrencyRef = new ReferenceType()
                {
                    name = "United States Dollar",
                    Value = "USD"
                };
                liabilityAccount = dataService.Add<Account>(accountp);
            }
            #endregion
            vendorCredit.APAccountRef = new ReferenceType()
            {
                name = liabilityAccount.Name,
                Value = liabilityAccount.Id
            };

            //Add Vendor credit details
            vendorCredit.TotalAmt = new Decimal(50.00);
            vendorCredit.TotalAmtSpecified = true;
            vendorCredit.TxnDate = DateTime.UtcNow.Date;
            vendorCredit.TxnDateSpecified = true;

            //Create a line and add it to the Vendor Credit
            List<Line> lineList = new List<Line>();
            Line line = new Line();
            line.Description = "Description";
            line.Amount = new Decimal(50.00);
            line.AmountSpecified = true;
            line.DetailType = LineDetailTypeEnum.AccountBasedExpenseLineDetail;
            line.DetailTypeSpecified = true;

            #region create expense account
            //Get a Expense account. If not present create one
            Account expenseAccount = accountQuerySvc.ExecuteIdsQuery("SELECT * FROM Account WHERE AccountType='Expense' AND Classification='Expense'").FirstOrDefault();
            if (expenseAccount == null)
            {
                Account accounte = new Account();
                String guid = Guid.NewGuid().ToString("N");
                accounte.Name = "Name_" + guid;

               // accounte.FullyQualifiedName = expenseAccount.Name;

                accounte.Classification = AccountClassificationEnum.Liability;
                accounte.ClassificationSpecified = true;
                accounte.AccountType = AccountTypeEnum.AccountsPayable;
                accounte.AccountTypeSpecified = true;

                accounte.CurrencyRef = new ReferenceType()
                {
                    name = "United States Dollar",
                    Value = "USD"
                };
                expenseAccount = dataService.Add<Account>(accounte);
            }
            #endregion


            line.AnyIntuitObject = new AccountBasedExpenseLineDetail()
            {
                AccountRef = new ReferenceType() { name = expenseAccount.Name, Value = expenseAccount.Id }
            };
            lineList.Add(line);
            vendorCredit.Line = lineList.ToArray();
            VendorCredit vendorcreditAdded = dataService.Add<VendorCredit>(vendorCredit);

            return vendorcreditAdded;
        }
        #endregion

        #region create billpayment

        /*
         * Create a BillPayment. 
         * A BillPayment object represents the payment transaction for a bill that the business owner receives from a vendor for goods or services purchased from the vendor. 
         * QuickBooks Online supports bill payments through a credit card or a checking account. 
         */
        private static BillPayment CreateBillPaymentCreditCard(ServiceContext serviceContext, Vendor vendor, Bill bill)
        {
            DataService dataService = new DataService(serviceContext);
            QueryService<Account> accountQuerySvc = new QueryService<Account>(serviceContext);

            //Create a bill payment and associate it to the vendor. Add VendorCredit
            BillPayment billPayment = new BillPayment();
            billPayment.PayType = BillPaymentTypeEnum.Check;
            billPayment.PayTypeSpecified = true;
            billPayment.TotalAmt = 300;
            billPayment.TotalAmtSpecified = true;
            billPayment.TxnDate = DateTime.UtcNow.Date;
            billPayment.TxnDateSpecified = true;
            billPayment.PrivateNote = "PrivateNote";
            billPayment.VendorRef = new ReferenceType()
            {
                name = vendor.DisplayName,
                type = "Vendor",
                Value = vendor.Id
            };

            #region create bank account
            //Create a Bank Account of type Credit Card. The bill payment will be via this account. If not present create a credit card account
            Account bankAccount = accountQuerySvc.ExecuteIdsQuery("SELECT * FROM Account WHERE AccountType='Credit Card' AND Classification='Liability'").FirstOrDefault();
            if (bankAccount == null)
            {
                Account accountb = new Account();
                String guid = Guid.NewGuid().ToString("N");
                accountb.Name = "Name_" + guid;

               // accountb.FullyQualifiedName = bankAccount.Name;

                accountb.Classification = AccountClassificationEnum.Liability;
                accountb.ClassificationSpecified = true;
                accountb.AccountType = AccountTypeEnum.CreditCard;
                accountb.AccountTypeSpecified = true;

                accountb.CurrencyRef = new ReferenceType()
                {
                    name = "United States Dollar",
                    Value = "USD"
                };
                bankAccount = dataService.Add<Account>(accountb);
            }
            #endregion

            BillPaymentCreditCard billPaymentCreditCard = new BillPaymentCreditCard();
            billPaymentCreditCard.CCAccountRef = new ReferenceType()
            {
                name = bankAccount.Name,
                Value = bankAccount.Id
            };

            CreditCardPayment creditCardPayment = new CreditCardPayment();
            creditCardPayment.CreditChargeInfo = new CreditChargeInfo()
            {
                Amount = new Decimal(10.00),
                AmountSpecified = true,
                Number = "124124124",
                NameOnAcct = bankAccount.Name,
                CcExpiryMonth = 10,
                CcExpiryMonthSpecified = true,
                CcExpiryYear = 2015,
                CcExpiryYearSpecified = true,
                BillAddrStreet = "BillAddrStreetba7cca47",
                PostalCode = "560045",
                CommercialCardCode = "CardCodeba7cca47",
                CCTxnMode = CCTxnModeEnum.CardPresent,
                CCTxnType = CCTxnTypeEnum.Charge
            };

            billPaymentCreditCard.CCDetail = creditCardPayment;
            billPayment.AnyIntuitObject = billPaymentCreditCard;

            //Create a line and it to the BillPayment
            List<Line> lineList = new List<Line>();
            Line line1 = new Line();
            line1.Amount = bill.TotalAmt;
            line1.AmountSpecified = true;
            List<LinkedTxn> LinkedTxnList1 = new List<LinkedTxn>();
            LinkedTxn linkedTxn1 = new LinkedTxn();
            linkedTxn1.TxnId = bill.Id;
            linkedTxn1.TxnType = TxnTypeEnum.Bill.ToString();
            LinkedTxnList1.Add(linkedTxn1);
            line1.LinkedTxn = LinkedTxnList1.ToArray();
            lineList.Add(line1);
            Line line = new Line();
            line.Amount = 300;
            line.AmountSpecified = true;
            List<LinkedTxn> LinkedTxnList = new List<LinkedTxn>();
            LinkedTxn linkedTxn = new LinkedTxn();
            linkedTxn.TxnId = bill.Id;
            linkedTxn.TxnType = TxnTypeEnum.VendorCredit.ToString();
            LinkedTxnList.Add(linkedTxn);
            line.LinkedTxn = LinkedTxnList.ToArray();
            lineList.Add(line);
            billPayment.Line = lineList.ToArray();
            BillPayment billPaymentAdded = dataService.Add<BillPayment>(billPayment);
            return billPaymentAdded;
        }
    }
    #endregion
}