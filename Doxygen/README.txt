- open doxygen.app
- load the Doxygen/doxygen configuration file
- delete the Mirror/Assets/Runtime/Transports folder once
- Run doxygen
- Show HTML output to verify it locally

- upload the Doxygen/html folder to our google cloud bucket:
  setup: https://cloud.google.com/storage/docs/hosting-static-website
  site:  https://storage.googleapis.com/mirror-api-docs/html/index.html