import { ModuleRegistry } from "ag-grid-community"
import { AllEnterpriseModule, LicenseManager } from "ag-grid-enterprise"

ModuleRegistry.registerModules([AllEnterpriseModule])

const key = import.meta.env.VITE_AG_GRID_LICENSE_KEY
if (key) LicenseManager.setLicenseKey(key)
    console.log("AG Grid key loaded:", Boolean(import.meta.env.VITE_AG_GRID_LICENSE_KEY))