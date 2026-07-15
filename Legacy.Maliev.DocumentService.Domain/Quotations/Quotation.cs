// <copyright file="Quotation.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.Quotations
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Quotation.
    /// </summary>
    public class Quotation
    {
        /// <summary>
        /// Gets or sets the comment.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the created date.
        /// </summary>
        /// <value>
        /// The created date.
        /// </value>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the currency.
        /// </summary>
        /// <value>
        /// The currency.
        /// </value>
        public string Currency { get; set; }

        /// <summary>
        /// Gets or sets the customer.
        /// </summary>
        /// <value>
        /// The customer.
        /// </value>
        public Customer Customer { get; set; } = new Customer();

        /// <summary>
        /// Gets or sets the employee.
        /// </summary>
        /// <value>
        /// The employee.
        /// </value>
        public Employee Employee { get; set; } = new Employee();

        /// <summary>
        /// Gets or sets the expiration date.
        /// </summary>
        /// <value>
        /// The expiration date.
        /// </value>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the fob.
        /// </summary>
        /// <value>
        /// The fob.
        /// </value>
        public string Fob { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the invoice number.
        /// </summary>
        /// <value>
        /// The invoice number.
        /// </value>
        public string InvoiceNumber { get; set; }

        /// <summary>
        /// Gets or sets the orders.
        /// </summary>
        /// <value>
        /// The orders.
        /// </value>
        public List<Order> Orders { get; set; } = new List<Order>();

        /// <summary>
        /// Gets or sets the period.
        /// </summary>
        /// <value>
        /// The period.
        /// </value>
        public int Period { get; set; }

        /// <summary>
        /// Gets or sets the quoted amount.
        /// </summary>
        /// <value>
        /// The quoted amount.
        /// </value>
        public decimal? QuotedAmount { get; set; }

        /// <summary>
        /// Gets or sets the shipped via.
        /// </summary>
        /// <value>
        /// The shipped via.
        /// </value>
        public string ShippedVia { get; set; }

        /// <summary>
        /// Gets or sets the subtotal.
        /// </summary>
        /// <value>
        /// The subtotal.
        /// </value>
        public decimal Subtotal { get; set; }

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
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        public decimal Vat { get; set; }

        /// <summary>
        /// Gets or sets the withholding tax.
        /// </summary>
        /// <value>
        /// The withholding tax.
        /// </value>
        public decimal? WithholdingTax { get; set; }
    }
}