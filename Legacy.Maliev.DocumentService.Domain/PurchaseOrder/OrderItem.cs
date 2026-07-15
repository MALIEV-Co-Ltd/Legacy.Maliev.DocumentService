// <copyright file="OrderItem.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.PurchaseOrder
{
    /// <summary>
    /// OrderItem.
    /// </summary>
    public class OrderItem
    {
        /// <summary>
        /// Gets or sets the currency.
        /// </summary>
        /// <value>
        /// The currency.
        /// </value>
        public string Currency { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the part number.
        /// </summary>
        /// <value>
        /// The part number.
        /// </value>
        public string PartNumber { get; set; }

        /// <summary>
        /// Gets or sets the quantity.
        /// </summary>
        /// <value>
        /// The quantity.
        /// </value>
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the subtotal.
        /// </summary>
        /// <value>
        /// The subtotal.
        /// </value>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Gets or sets the unit price.
        /// </summary>
        /// <value>
        /// The unit price.
        /// </value>
        public decimal UnitPrice { get; set; }
    }
}