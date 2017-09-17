# Bulk Photo Edit
[![Build status](https://ci.appveyor.com/api/projects/status/vyp9d9bty1pgpnix?svg=true)](https://ci.appveyor.com/project/adam-azarchs/bulk-exif-edit)

This is a tool to make changes to the exif tags for potentially large sets of
photos.  The following actions are supported:

* Rotate images according to their EXIF orientation metadata, and reset the
orientation.
* Shift the "Date Taken" exif metadata by a uniform amount.
* Add latitude and longitude metadata.
* Set the dpi metadata.
