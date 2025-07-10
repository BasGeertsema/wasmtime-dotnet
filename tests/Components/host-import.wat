(component
  (import "test-host" (instance $host
    (export "add-numbers" (func (param "x" s32) (param "y" s32) (result s32)))
  ))
  
  (core module $m
    (import "host" "add" (func $host_add (param i32 i32) (result i32)))
    
    (func (export "call-host-add") (param i32 i32) (result i32)
      local.get 0
      local.get 1
      call $host_add
    )
  )
  
  (core func $add (canon lower (func $host "add-numbers")))
  
  (core instance $i (instantiate $m
    (with "host" (instance
      (export "add" (func $add))
    ))
  ))
  
  (func (export "test-add") (param "x" s32) (param "y" s32) (result s32)
    (canon lift (core func $i "call-host-add"))
  )
)