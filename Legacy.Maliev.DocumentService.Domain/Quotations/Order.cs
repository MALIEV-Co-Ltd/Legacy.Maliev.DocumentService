// <copyright file="Order.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.Quotations
{
    /// <summary>
    /// Order.
    /// </summary>
    public class Order
    {
        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the discount.
        /// </summary>
        /// <value>
        /// The discount.
        /// </value>
        public decimal? Discount { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the lead time.
        /// </summary>
        /// <value>
        /// The lead time.
        /// </value>
        public int? LeadTime { get; set; }

        /// <summary>
        /// Gets or sets the material.
        /// </summary>
        /// <value>
        /// The material.
        /// </value>
        public string Material { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

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
        /// Gets or sets the surface finish.
        /// </summary>
        /// <value>
        /// The surface finish.
        /// </value>
        public string SurfaceFinish { get; set; }

        /// <summary>
        /// Gets or sets the unit price.
        /// </summary>
        /// <value>
        /// The unit price.
        /// </value>
        public decimal UnitPrice { get; set; }
    }
}