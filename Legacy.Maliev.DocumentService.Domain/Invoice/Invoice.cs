// <copyright file="Invoice.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.Invoice
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Invoice Model.
    /// </summary>
    public class Invoice
    {
        /// <summary>
        /// Gets or sets the billing address building.
        /// </summary>
        /// <value>
        /// The billing address building.
        /// </value>
        public string BillingAddressBuilding { get; set; }

        /// <summary>
        /// Gets or sets the billing address city.
        /// </summary>
        /// <value>
        /// The billing address city.
        /// </value>
        public string BillingAddressCity { get; set; }

        /// <summary>
        /// Gets or sets the billing address company.
        /// </summary>
        /// <value>
        /// The billing address company.
        /// </value>
        public string BillingAddressCompany { get; set; }

        /// <summary>
        /// Gets or sets the billing address country.
        /// </summary>
        /// <value>
        /// The billing address country.
        /// </value>
        public string BillingAddressCountry { get; set; }

        /// <summary>
        /// Gets or sets the billing address line1.
        /// </summary>
        /// <value>
        /// The billing address line1.
        /// </value>
        public string BillingAddressLine1 { get; set; }

        /// <summary>
        /// Gets or sets the billing address line2.
        /// </summary>
        /// <value>
        /// The billing address line2.
        /// </value>
        public string BillingAddressLine2 { get; set; }

        /// <summary>
        /// Gets or sets the billing address postal code.
        /// </summary>
        /// <value>
        /// The billing address postal code.
        /// </value>
        public string BillingAddressPostalCode { get; set; }

        /// <summary>
        /// Gets or sets the billing address recipient.
        /// </summary>
        /// <value>
        /// The billing address recipient.
        /// </value>
        public string BillingAddressRecipient { get; set; }

        /// <summary>
        /// Gets or sets the state of the billing address.
        /// </summary>
        /// <value>
        /// The state of the billing address.
        /// </value>
        public string BillingAddressState { get; set; }

        /// <summary>
        /// Gets or sets the commercial registration.
        /// </summary>
        /// <value>
        /// The commercial registration.
        /// </value>
        public string CommercialRegistration { get; set; }

        /// <summary>
        /// Gets or sets the created date.
        /// </summary>
        /// <value>
        /// The created date.
        /// </value>
        public DateTime? CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the currency.
        /// </summary>
        /// <value>
        /// The currency.
        /// </value>
        public string Currency { get; set; }

        /// <summary>
        /// Gets or sets the customer identifier.
        /// </summary>
        /// <value>
        /// The customer identifier.
        /// </value>
        public int CustomerId { get; set; }

        /// <summary>
        /// Gets or sets the fob.
        /// </summary>
        /// <value>
        /// The fob.
        /// </value>
        public string Fob { get; set; }

        /// <summary>
        /// Gets or sets the number.
        /// </summary>
        /// <value>
        /// The number.
        /// </value>
        public string Number { get; set; }

        /// <summary>
        /// Gets or sets the order items.
        /// </summary>
        /// <value>
        /// The order items.
        /// </value>
        public List<OrderItem> OrderItems { get; set; }

        /// <summary>
        /// Gets or sets the outstanding.
        /// </summary>
        /// <value>
        /// The outstanding.
        /// </value>
        public decimal? Outstanding { get; set; }

        /// <summary>
        /// Gets or sets the purchase order number.
        /// </summary>
        /// <value>
        /// The purchase order number.
        /// </value>
        public string PurchaseOrderNumber { get; set; }

        /// <summary>
        /// Gets or sets the comment.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        public string Remark { get; set; }

        /// <summary>
        /// Gets or sets the requisitioner.
        /// </summary>
        /// <value>
        /// The requisitioner.
        /// </value>
        public string Requisitioner { get; set; }

        /// <summary>
        /// Gets or sets the sales person.
        /// </summary>
        /// <value>
        /// The sales person.
        /// </value>
        public string SalesPerson { get; set; }

        /// <summary>
        /// Gets or sets the shipped via.
        /// </summary>
        /// <value>
        /// The shipped via.
        /// </value>
        public string ShippedVia { get; set; }

        /// <summary>
        /// Gets or sets the shipping address building.
        /// </summary>
        /// <value>
        /// The shipping address building.
        /// </value>
        public string ShippingAddressBuilding { get; set; }

        /// <summary>
        /// Gets or sets the shipping address city.
        /// </summary>
        /// <value>
        /// The shipping address city.
        /// </value>
        public string ShippingAddressCity { get; set; }

        /// <summary>
        /// Gets or sets the shipping address company.
        /// </summary>
        /// <value>
        /// The shipping address company.
        /// </value>
        public string ShippingAddressCompany { get; set; }

        /// <summary>
        /// Gets or sets the shipping address country.
        /// </summary>
        /// <value>
        /// The shipping address country.
        /// </value>
        public string ShippingAddressCountry { get; set; }

        /// <summary>
        /// Gets or sets the shipping address line1.
        /// </summary>
        /// <value>
        /// The shipping address line1.
        /// </value>
        public string ShippingAddressLine1 { get; set; }

        /// <summary>
        /// Gets or sets the shipping address line2.
        /// </summary>
        /// <value>
        /// The shipping address line2.
        /// </value>
        public string ShippingAddressLine2 { get; set; }

        /// <summary>
        /// Gets or sets the shipping address postal code.
        /// </summary>
        /// <value>
        /// The shipping address postal code.
        /// </value>
        public string ShippingAddressPostalCode { get; set; }

        /// <summary>
        /// Gets or sets the shipping address recipient.
        /// </summary>
        /// <value>
        /// The shipping address recipient.
        /// </value>
        public string ShippingAddressRecipient { get; set; }

        /// <summary>
        /// Gets or sets the recipient telephone number.
        /// </summary>
        /// <value>
        /// The recipient telephone number.
        /// </value>
        public string ShippingAddressRecipientTelephone { get; set; }

        /// <summary>
        /// Gets or sets the state of the shipping address.
        /// </summary>
        /// <value>
        /// The state of the shipping address.
        /// </value>
        public string ShippingAddressState { get; set; }

        /// <summary>
        /// Gets or sets the subtotal.
        /// </summary>
        /// <value>
        /// The subtotal.
        /// </value>
        public decimal? Subtotal { get; set; }

        /// <summary>
        /// Gets or sets the tax identification.
        /// </summary>
        /// <value>
        /// The tax identification.
        /// </value>
        public string TaxIdentification { get; set; }

        /// <summary>
        /// Gets or sets the terms.
        /// </summary>
        /// <value>
        /// The terms.
        /// </value>
        public string Terms { get; set; }

        /// <summary>
        /// Gets or sets the total.
        /// </summary>
        /// <value>
        /// The total.
        /// </value>
        public decimal? Total { get; set; }

        /// <summary>
        /// Gets or sets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        public decimal? Vat { get; set; }

        /// <summary>
        /// Gets or sets the withholding tax.
        /// </summary>
        /// <value>
        /// The service charge.
        /// </value>
        public decimal? WithholdingTax { get; set; }
    }
}