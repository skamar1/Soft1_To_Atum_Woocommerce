<?php

/**
 * Stores Configuration
 * Ρυθμίσεις για κάθε κατάστημα που θα συγχρονίζεται
 */

return [
    'store1' => [
        'name' => 'Κατάστημα Κέντρο',
        'enabled' => true,
        'sync_interval' => 15, // minutes

        // SoftOne Go Settings
        'softone_go' => [
            'base_url' => 'https://go.s1cloud.net/s1services',
            'app_id' => '703',
            'token' => '9J8pIbTHLLLI9JT4TLLoHKLIL4rtLrHvHLXQLNHJLMLKLsTLK5bMMKK',
            's1code' => '10503446903725',
            'filters' => 'ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999'
        ],

        // ATUM Multi Inventory Settings
        'atum' => [
            'location_id' => 870,
            'location_name' => 'store1_location'
        ],

        // WooCommerce Settings
        'woocommerce' => [
            'url' => 'https://panes.gr',
            'consumer_key' => 'ck_4f005295733d647c2ca268cedecb92eb6498d54e',
            'consumer_secret' => 'cs_c8fc08cd54f9018e20eec104bda4868eedfde347',
            'version' => 'wc/v3'
        ],

        // Product Matching Settings
        'matching' => [
            'primary_field' => 'sku', // sku, barcode, code
            'secondary_field' => 'barcode',
            'create_missing_products' => true,
            'update_existing_products' => true
        ],

        // Field Mapping (SoftOne Go field -> WooCommerce field)
        'field_mapping' => [
            'sku' => 'ITEM.CODE1', // Barcode από SoftOne Go
            'name' => 'ITEM.NAME',
            'price' => 'ITEM.PRICER',
            'stock_quantity' => 'ITEM.MTRL_ITEMTRDATA_QTY1',
            'category' => 'ITEM.MTRCATEGORY',
            'unit' => 'ITEM.MTRUNIT1',
            'vat' => 'ITEM.VAT'
        ]
    ],

    'store2' => [
        'name' => 'Κατάστημα Βορειων Προαστίων',
        'enabled' => false, // Disabled for now
        'sync_interval' => 15,

        'softone_go' => [
            'base_url' => 'https://go.s1cloud.net/s1services',
            'app_id' => 'YOUR_APP_ID_2',
            'token' => 'YOUR_TOKEN_2',
            's1code' => 'YOUR_S1CODE_2',
            'filters' => 'ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999'
        ],

        'atum' => [
            'location_id' => 871,
            'location_name' => 'store2_location'
        ],

        'woocommerce' => [
            'url' => 'https://panes.gr',
            'consumer_key' => 'ck_xxxxx',
            'consumer_secret' => 'cs_xxxxx',
            'version' => 'wc/v3'
        ],

        'matching' => [
            'primary_field' => 'sku',
            'secondary_field' => 'barcode',
            'create_missing_products' => true,
            'update_existing_products' => true
        ],

        'field_mapping' => [
            'sku' => 'ITEM.CODE1',
            'name' => 'ITEM.NAME',
            'price' => 'ITEM.PRICER',
            'stock_quantity' => 'ITEM.MTRL_ITEMTRDATA_QTY1',
            'category' => 'ITEM.MTRCATEGORY',
            'unit' => 'ITEM.MTRUNIT1',
            'vat' => 'ITEM.VAT'
        ]
    ]
];