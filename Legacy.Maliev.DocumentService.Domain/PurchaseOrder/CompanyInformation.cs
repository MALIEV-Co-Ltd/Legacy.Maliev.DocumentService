// <copyright file="CompanyInformation.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.PurchaseOrder
{
    /// <summary>
    /// Company Information.
    /// </summary>
    public class CompanyInformation
    {
        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        /// <value>
        /// The supplier address.
        /// </value>
        public Address Address { get; set; }

        /// <summary>
        /// Gets or sets the name of the company.
        /// </summary>
        /// <value>
        /// The name of the company.
        /// </value>
        public string CompanyName { get; set; }

        /// <summary>
        /// Gets or sets the name of the contact.
        /// </summary>
        /// <value>
        /// The name of the contact.
        /// </value>
        public string ContactName { get; set; }

        /// <summary>
        /// Gets or sets the fax.
        /// </summary>
        /// <value>
        /// The fax.
        /// </value>
        public string Fax { get; set; }

        /// <summary>
        /// Gets or sets the mobile.
        /// </summary>
        /// <value>
        /// The mobile.
        /// </value>
        public string Mobile { get; set; }

        /// <summary>
        /// Gets or sets the telephone.
        /// </summary>
        /// <value>
        /// The telephone.
        /// </value>
        public string Telephone { get; set; }
    }
}