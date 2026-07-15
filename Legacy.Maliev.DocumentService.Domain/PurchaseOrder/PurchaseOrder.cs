// <copyright file="PurchaseOrder.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.PurchaseOrder
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Purchase Order.
    /// </summary>
    public class PurchaseOrder
    {
        /// <summary>
        /// Gets or sets the billing.
        /// </summary>
        /// <value>
        /// The billing.
        /// </value>
        public CompanyInformation Billing { get; set; }

        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        /// <value>
        /// The date.
        /// </value>
        public DateTime Date { get; set; }

        /// <summary>
        /// Gets or sets the fob.
        /// </summary>
        /// <value>
        /// The fob.
        /// </value>
        public string FOB { get; set; }

        /// <summary>
        /// Gets or sets the notes.
        /// </summary>
        /// <value>
        /// The notes.
        /// </value>
        public string Notes { get; set; }

        /// <summary>
        /// Gets or sets the ordered by.
        /// </summary>
        /// <value>
        /// The ordered by.
        /// </value>
        public string OrderedBy { get; set; }

        /// <summary>
        /// Gets or sets the order items.
        /// </summary>
        /// <value>
        /// The order items.
        /// </value>
        public List<OrderItem> OrderItems { get; set; }

        /// <summary>
        /// Gets or sets the reference number.
        /// </summary>
        /// <value>
        /// The reference number.
        /// </value>
        public int ReferenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the shipped via.
        /// </summary>
        /// <value>
        /// The shipped via.
        /// </value>
        public string ShippedVia { get; set; }

        /// <summary>
        /// Gets or sets the shipping.
        /// </summary>
        /// <value>
        /// The shipping.
        /// </value>
        public CompanyInformation Shipping { get; set; }

        /// <summary>
        /// Gets or sets the supplier.
        /// </summary>
        /// <value>
        /// The supplier.
        /// </value>
        public CompanyInformation Supplier { get; set; }

        /// <summary>
        /// Gets or sets the terms.
        /// </summary>
        /// <value>
        /// The terms.
        /// </value>
        public string Terms { get; set; }
    }
}