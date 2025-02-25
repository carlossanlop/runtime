 # IMPORTANT: do not use add_compile_options(), add_definitions() or similar functions here since it will leak to the including projects

 include(FetchContent)

 FetchContent_Declare(
    fetchzstd
    SOURCE_DIR "${CMAKE_CURRENT_LIST_DIR}/zstd"
 )

set(BUILD_SHARED_LIBS OFF) # Shared libraries aren't supported in wasm

set(SKIP_INSTALL_ALL ON)
FetchContent_MakeAvailable(fetchzlibng)
set(SKIP_INSTALL_ALL OFF)
