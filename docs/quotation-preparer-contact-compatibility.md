# Quotation preparer contacts

The retained quotation generator rendered employee Telephone, Mobile and Fax
when supplied. The public Employee DTO still accepts all three PascalCase fields,
but the QuestPDF preparer column omitted Telephone and Fax. The composer now
includes both with the existing optional-field behavior; absent values add no
labels. Existing employee email behavior is preserved.

The normal Production HTTP/PDF regression uses distinct customer and preparer
contacts, scopes assertions to the preparer column, checks the actual telephone
and fax values, and checks absence without allowing the customer's contacts to
mask missing preparer values. The prior364 executions and156 focused tests,
22 frozen PDF oracles and29 frozen assets remain required and unchanged.

This is a code compatibility correction, with no provider, database, image,
deployment, private customer-data restoration or whole-source closure claim.
